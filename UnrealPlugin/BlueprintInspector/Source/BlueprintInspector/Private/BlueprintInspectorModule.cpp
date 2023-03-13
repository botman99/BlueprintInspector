// Copyright 2023 - Jeffrey "botman" Broome. All Rights Reserved

#include "BlueprintInspectorModule.h"

#include "Editor.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "Subsystems/AssetEditorSubsystem.h"
#include "Async/Async.h"
#include "Misc/FileHelper.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "BlueprintAssetHandler.h"
#include "K2Node_Switch.h"
#include "K2Node_Event.h"
#include "K2Node_FunctionEntry.h"
#include "K2Node_CallFunction.h"
#include "K2Node_CallArrayFunction.h"

#if PLATFORM_WINDOWS
#include "Windows/AllowWindowsPlatformTypes.h"
#endif

DEFINE_LOG_CATEGORY(LogBlueprintInspector);

bool FBlueprintInspectorModule::bCanReadFromPipe = false;

TArray<FName> FBlueprintInspectorModule::ActorFunctions;
TMap<FName, UClass*> FBlueprintInspectorModule::ClassNameToClassPointer;

FCriticalSection FBlueprintInspectorModule::BlueprintInspectorMutex;

TArray<FString> FBlueprintInspectorModule::AssetPaths;
bool FBlueprintInspectorModule::bAssetsReadyToProcess = false;

UObject* FBlueprintInspectorModule::BlueprintObject = nullptr;
UClass* FBlueprintInspectorModule::BlueprintSearchClass = nullptr;
FName FBlueprintInspectorModule::BlueprintSearchFunction = NAME_None;
bool FBlueprintInspectorModule::bNeedToSearchForFunction = false;

#if PLATFORM_WINDOWS
static int32 handle;
#endif

void FBlueprintInspectorModule::StartupModule()
{
#if WITH_EDITOR
	ActorFunctions.Empty();
	UClass* ActorClass = AActor::StaticClass();
	for (TFieldIterator<UFunction> FuncIter(ActorClass, EFieldIteratorFlags::ExcludeSuper); FuncIter; ++FuncIter)
	{
		if ((*FuncIter)->HasAnyFunctionFlags(FUNC_Native) && (*FuncIter)->HasAnyFunctionFlags(FUNC_BlueprintCallable | FUNC_BlueprintEvent | FUNC_BlueprintPure | FUNC_BlueprintAuthorityOnly | FUNC_BlueprintCosmetic))
		{
			if (FuncIter == nullptr)
			{
				continue;  // should never happen
			}

			ActorFunctions.AddUnique((*FuncIter)->GetFName());
		}
	}

	FBlueprintInspectorModule::ClassNameToClassPointer.Empty();
	for (TObjectIterator<UClass> ClassIt; ClassIt; ++ClassIt)
	{
		UClass* SomeClass = *ClassIt;
		ClassNameToClassPointer.Add(SomeClass->GetFName(), SomeClass);
	}

#if PLATFORM_WINDOWS
	BOOL result = ::AllowSetForegroundWindow(-1);  // ASFW_ANY (allow any external process to set focus to us, this allows Visual Studio to SetFocus on the editor when opening a Blueprint asset)
	if (result == 0)
	{
		DWORD err = ::GetLastError();
		UE_LOG(LogBlueprintInspector, Log, TEXT("FBlueprintInspectorModule::StartupModule() - GetLastError = %d"), err);
	}
#endif  // PLATFORM_WINDOWS

	if (!IsRunningCommandlet())
	{
		FAssetRegistryModule& AssetRegistryModule = FModuleManager::LoadModuleChecked<FAssetRegistryModule>(TEXT("AssetRegistry"));
		AssetRegistryModule.Get().OnFilesLoaded().AddRaw(this, &FBlueprintInspectorModule::OnAssetRegistryFilesLoaded);

		FTickerDelegate Delegate = FTickerDelegate::CreateRaw(this, &FBlueprintInspectorModule::CheckForAssetToOpen);
#if (ENGINE_MAJOR_VERSION == 4) && (ENGINE_MINOR_VERSION == 27)
		TickerHandle = FTicker::GetCoreTicker().AddTicker(Delegate, 0.1f);
#else
		TickerHandle = FTSTicker::GetCoreTicker().AddTicker(Delegate, 0.1f);
#endif

		ReadPipeThreadFuture = Async(EAsyncExecution::Thread, [this]() { ReadPipeThreadProc(); });
	}
#endif
}

void FBlueprintInspectorModule::ShutdownModule()
{
#if WITH_EDITOR
#if (ENGINE_MAJOR_VERSION == 4) && (ENGINE_MINOR_VERSION == 27)
	FTicker::GetCoreTicker().RemoveTicker(TickerHandle);
#else
	FTSTicker::GetCoreTicker().RemoveTicker(TickerHandle);
#endif
#endif
}

#if WITH_EDITOR
void FBlueprintInspectorModule::GatherK2NodesForAsset(UObject* BlueprintAsset, EObjectFlags ObjectFlag, TArray<UK2Node*>& K2Nodes)
{
	// gather the list of all the K2Nodes used in this Blueprint (to determine how many times a function call exists in each Blueprint)
	K2Nodes.Empty();

	UPackage* BlueprintPackage = BlueprintAsset->GetPackage();
	if (BlueprintPackage == nullptr)
	{
		return;
	}

	for(TObjectIterator<UK2Node> It; It; ++It)
	{
		UK2Node* Node = *It;

		if (!IsValid(Node))  // UE4: Node->IsPendingKill()
		{
			continue;
		}

		if (Node->IsInPackage(BlueprintPackage))
		{
			if (!Node->HasAnyFlags(ObjectFlag))
			{
				continue;
			}

			K2Nodes.AddUnique(Node);
		}
	}
}

void FBlueprintInspectorModule::GetK2NodeClassFunction(UBlueprintGeneratedClass* BlueprintGeneratedClass, UK2Node* K2Node, UClass*& NodeClass, FName& NodeFunction)
{
	NodeClass = nullptr;
	NodeFunction = NAME_None;

	UK2Node_Switch* Node_Switch = Cast<UK2Node_Switch>(K2Node);
	UK2Node_Event* Node_Event = Cast<UK2Node_Event>(K2Node);
	UK2Node_FunctionEntry* Node_FunctionEntry = Cast<UK2Node_FunctionEntry>(K2Node);
	UK2Node_CallFunction* Node_CallFunction = Cast<UK2Node_CallFunction>(K2Node);
	UK2Node_CallArrayFunction* Node_CallArrayFunction = Cast<UK2Node_CallArrayFunction>(K2Node);

	if (Node_Switch)
	{
		NodeClass = Node_Switch->FunctionClass.Get();
		NodeFunction = Node_Switch->FunctionName;
	}
	else if (Node_Event)
	{
		NodeClass = Node_Event->EventReference.GetMemberParentClass();
		NodeFunction = Node_Event->EventReference.GetMemberName();
	}
	else if (Node_FunctionEntry)
	{
		NodeClass = Node_FunctionEntry->FunctionReference.GetMemberParentClass();
		NodeFunction = Node_FunctionEntry->FunctionReference.GetMemberName();
	}
	else if (Node_CallFunction)
	{
		NodeClass = Node_CallFunction->FunctionReference.GetMemberParentClass();
		NodeFunction = Node_CallFunction->FunctionReference.GetMemberName();
	}
	else if (Node_CallArrayFunction)
	{
		NodeClass = Node_CallArrayFunction->FunctionReference.GetMemberParentClass();
		NodeFunction = Node_CallArrayFunction->FunctionReference.GetMemberName();
	}

	if ((NodeClass == nullptr) && (NodeFunction != NAME_None))  // special case if the NodeClass is null but NodeFunction is valid
	{
		if (!NodeFunction.ToString().StartsWith(TEXT("ExecuteUbergraph")))
		{
			// see if the Blueprint's CalledFunctions contains a matching function name...
			for (UFunction* Func : BlueprintGeneratedClass->CalledFunctions)  // iterate through all the Blueprint's called functions
			{
				if (Func && Func->HasAnyFunctionFlags(FUNC_Native))  // skip this function is if isn't native C++ code
				{
					if (Func->GetFName() == NodeFunction)
					{
						NodeClass = Func->GetOwnerClass();
					}
				}
			}
		}
	}

	if ((NodeClass == nullptr) && (NodeFunction != NAME_None))  // if we still can't find the class, see if the function exists in Actor class...
	{
		if (FBlueprintInspectorModule::ActorFunctions.Contains(NodeFunction))
		{
			NodeClass = AActor::StaticClass();
		}
	}
}

void FBlueprintInspectorModule::OnAssetRegistryFilesLoaded()
{
	bIsAssetRegistryLoaded = true;
}

void FBlueprintInspectorModule::ReadPipeThreadProc()
{
	while (!bIsAssetRegistryLoaded)
	{
		FPlatformProcess::Sleep(0.1f);
	}

#if PLATFORM_WINDOWS
	uint32 ProcessId = ::GetCurrentProcessId();

	handle = PtrToInt(FWindowsPlatformMisc::GetTopLevelWindowHandle(ProcessId));
	UE_LOG(LogBlueprintInspector, Log, TEXT("FBlueprintInspectorModule::ReadPipeThreadProc() - handle = %08x"), handle);
#endif

	double WarningTime = 0.f;

	while (true)
	{
		bCanReadFromPipe = false;

		// try to open the pipe as a client (some other application must create the pipe first)
		if (!BlueprintInspectorNamedPipe.Create(FString::Printf(TEXT("\\\\.\\pipe\\BlueprintInspector")), false, false))
		{
			// try to open the pipe as a server (this will create the pipe, bi-directional by default, only one pipe of this name can be created at a time)
			if (BlueprintInspectorNamedPipe.Create(FString::Printf(TEXT("\\\\.\\pipe\\BlueprintInspector")), true, false))
			{
				if (BlueprintInspectorNamedPipe.OpenConnection())
				{
					// if named pipe was successfully opened as a server...
					bCanReadFromPipe = true;
				}
				else
				{
					if (WarningTime < FPlatformTime::Seconds())
					{
						UE_LOG(LogBlueprintInspector, Warning, TEXT("Failed to open connection to BlueprintInspector Named Pipe as a server."));
						WarningTime = FPlatformTime::Seconds() + 60.f;  // only warn once per minute
					}

					float SleepTime = 1.0f + (FMath::FRand() * 0.5f);
					FPlatformProcess::Sleep(SleepTime);
				}
			}
			else
			{
				if (WarningTime < FPlatformTime::Seconds())
				{
					UE_LOG(LogBlueprintInspector, Warning, TEXT("Failed to create BlueprintInspector Named Pipe as a server."));
					WarningTime = FPlatformTime::Seconds() + 60.f;  // only warn once per minute
				}

				float SleepTime = 1.0f + (FMath::FRand() * 0.5f);
				FPlatformProcess::Sleep(SleepTime);
			}
		}
		else
		{
			// if named pipe was successfully opened as a client...
			bCanReadFromPipe = true;
		}

		if (bCanReadFromPipe)
		{
			BlueprintInspectorNamedPipe.WriteBytes(4, &handle);  // write the top level window handle value
		}

		UE_LOG(LogBlueprintInspector, Log, TEXT("FBlueprintInspectorModule::ReadPipeThreadProc() - pipe connected."));

		while (bCanReadFromPipe)
		{
			BYTE lower_byte = 0;
			if (BlueprintInspectorNamedPipe.ReadBytes(1, &lower_byte)) // read lower byte of length
			{
				BYTE upper_byte = 0;
				if (BlueprintInspectorNamedPipe.ReadBytes(1, &upper_byte)) // read upper byte of length
				{
					int32 NumBytes = (short)upper_byte * 256 + (short)lower_byte;
					TArray<uint8> Buffer;
					Buffer.Init(0, NumBytes);

					if (BlueprintInspectorNamedPipe.ReadBytes(NumBytes, Buffer.GetData()))
					{
						FString Command;
						FFileHelper::BufferToString(Command, Buffer.GetData(), Buffer.Num());

						if (Command.StartsWith(TEXT("AssetPath")))
						{
							FScopeLock ScopeLock(&BlueprintInspectorMutex);
							AssetPaths.Add(Command);
							bAssetsReadyToProcess = true;
						}
					}
					else
					{
						bCanReadFromPipe = false;

						if (WarningTime < FPlatformTime::Seconds())
						{
							UE_LOG(LogBlueprintInspector, Warning, TEXT("ReadBytes for length %d failed (pipe closed?)."), NumBytes);
							WarningTime = FPlatformTime::Seconds() + 60.f;  // only warn once per minute
						}
					}
				}
				else
				{
					bCanReadFromPipe = false;

					if (WarningTime < FPlatformTime::Seconds())
					{
						UE_LOG(LogBlueprintInspector, Warning, TEXT("ReadBytes for length of message failed (upper_byte, pipe closed?)."));
						WarningTime = FPlatformTime::Seconds() + 60.f;  // only warn once per minute
					}
				}
			}
			else
			{
				bCanReadFromPipe = false;

				if (WarningTime < FPlatformTime::Seconds())
				{
					UE_LOG(LogBlueprintInspector, Warning, TEXT("ReadBytes for length of message failed (lower_byte, pipe closed?)."));
					WarningTime = FPlatformTime::Seconds() + 60.f;  // only warn once per minute
				}
			}
		}

		UE_LOG(LogBlueprintInspector, Log, TEXT("FBlueprintInspectorModule::ReadPipeThreadProc() - pipe disconnected."));

		BlueprintInspectorNamedPipe.Destroy();

		// sleep, then try to open the pipe again...
		float SleepTime = 1.0f + (FMath::FRand() * 0.5f);
		FPlatformProcess::Sleep(SleepTime);

	} // while (true)
}

bool FBlueprintInspectorModule::CheckForAssetToOpen(float FrameDeltaTime)  // note: FrameDeltaTime is delta time of last tick, not since last time called
{
	UAssetEditorSubsystem* AssetEditorSubsystem = GEditor->GetEditorSubsystem<UAssetEditorSubsystem>();

	if (!bIsAssetRegistryLoaded)  // wait until the Asset Registry has been completely loaded
	{
		return true;  // keep re-triggering
	}

	if (bAssetsReadyToProcess)
	{
		TArray<FString> AssetPathsToOpen;

		{
			FScopeLock ScopeLock(&BlueprintInspectorMutex);
			AssetPathsToOpen = AssetPaths;
			AssetPaths.Empty();
			bAssetsReadyToProcess = false;
		}

		FAssetRegistryModule& AssetRegistryModule = FModuleManager::LoadModuleChecked<FAssetRegistryModule>(TEXT("AssetRegistry"));

		for (int32 index = 0; index < AssetPathsToOpen.Num(); ++index)
		{
			TArray<FString> args;
			AssetPathsToOpen[index].ParseIntoArray(args, TEXT(","));

			if (args.Num() < 2)
			{
				continue;
			}

			TArray<FAssetData> Assets;
			if (AssetRegistryModule.Get().GetAssetsByPackageName(FName(args[1]), Assets))
			{
				if (Assets.Num() > 0)
				{
					if (AssetEditorSubsystem)
					{
						AssetEditorSubsystem->OpenEditorForAsset(Assets[0].GetAsset());

						BlueprintSearchClass = nullptr;
						BlueprintSearchFunction = NAME_None;

						BlueprintObject = Assets[0].GetAsset();
						if (args.Num() > 3)
						{
							FString TempClassName = args[2].RightChop(1);
							BlueprintSearchClass = ClassNameToClassPointer[FName(TempClassName)];
							BlueprintSearchFunction = FName(args[3]);
						}

						if (BlueprintSearchClass != nullptr)
						{
							bNeedToSearchForFunction = true;
						}
					}
				}
			}
		}
	}

	if (bNeedToSearchForFunction)
	{
		if (AssetEditorSubsystem->FindEditorForAsset(BlueprintObject, true) != nullptr)
		{
			bNeedToSearchForFunction = false;

			TArray<UK2Node*> K2Nodes;
			FBlueprintInspectorModule::GatherK2NodesForAsset(BlueprintObject, RF_Transactional, K2Nodes);  // RF_Transient for nodes loaded by the commandlet and RF_Transactional for nodes loaded by the Blueprint editor

			UClass* BlueprintClass = BlueprintObject->GetClass();
			if (BlueprintClass == nullptr)
			{
				return true;
			}

			const IBlueprintAssetHandler* Handler = FBlueprintAssetHandler::Get().FindHandler(BlueprintClass);
			UBlueprint* Blueprint = Handler ? Handler->RetrieveBlueprint(BlueprintObject) : nullptr;

			if (Blueprint == nullptr)
			{
				return true;
			}

			UBlueprintGeneratedClass* BlueprintGeneratedClass = Cast<UBlueprintGeneratedClass>(Blueprint->GeneratedClass);

			UK2Node* FoundK2Node = nullptr;

			for (int32 node_index = 0; node_index < K2Nodes.Num(); ++node_index)
			{
				UClass* NodeClass = nullptr;
				FName NodeFunction = NAME_None;
				FBlueprintInspectorModule::GetK2NodeClassFunction(BlueprintGeneratedClass, K2Nodes[node_index], NodeClass, NodeFunction);

				if ((NodeClass == BlueprintSearchClass) && (NodeFunction == BlueprintSearchFunction))
				{
					FoundK2Node = K2Nodes[node_index];
					break;
				}
			}

			if (FoundK2Node != nullptr)
			{
				FKismetEditorUtilities::BringKismetToFocusAttentionOnObject(FoundK2Node);
			}
		}
	}

	return true;  // keep re-triggering
}
#endif // WITH_EDITOR

#if PLATFORM_WINDOWS
#include "Windows/HideWindowsPlatformTypes.h"
#endif

IMPLEMENT_MODULE(FBlueprintInspectorModule, BlueprintInspector)
