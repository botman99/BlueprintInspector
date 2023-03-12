// Copyright Jeffrey "botman" Broome. All Rights Reserved

#include "BlueprintInspectorCommandlet.h"
#include "BlueprintInspector.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "BlueprintInspectorModule.h"
#include "BlueprintAssetHandler.h"
#include "Serialization/JsonWriter.h"
#include "Serialization/JsonSerializer.h"
#include "Policies/CondensedJsonPrintPolicy.h"
#include "Misc/FileHelper.h"

TArray<FString> BlueprintAssetPathNames;  // list of Blueprint Asset pathnames (i.e. "/Engine/path/asset", "/<PluginName>/path/asset", or "/Game/path/asset")

struct BlueprintAssetIndexInfo  // this is the index into the list of Blueprint assets (BlueprintAssetPathNames)
{
	uint32 BlueprintAssetIndex;  // which Blueprint this function appears in
	uint32 Count;  // how many times this function appears in the Blueprint
};

struct BlueprintFunctionInfo  // there is one of these per class Blueprint function
{
	FString FunctionName;
	TArray<BlueprintAssetIndexInfo> BlueprintAssetIndexInfos;
};

struct BlueprintClassInfo  // there is one of these per class
{
	UClass* BlueprintClass;
	FString ClassName;
	TMap<FString, int32> FunctionNameToInfoIndex;
	TArray<BlueprintFunctionInfo> FunctionInfos;  // Blueprint functions contained in this class
};


// Commandlet for generating the Blueprint function .json file
int32 UBlueprintInspectorCommandlet::Main(const FString& CommandLine)
{
	UE_LOG(LogBlueprintInspector, Log, TEXT("--------------------------------------------------------------------------------"));
	UE_LOG(LogBlueprintInspector, Log, TEXT("Running BlueprintInspector Commandlet"));

	int32 GCCounter = 0;  // counter used to periodically perform garbage collection

	bool bSkipEngine = false;
	bool bSkipPlugins = false;
	bool bSkipDevelopers = false;
	bool bSkipGame = false;

	TArray<FString> Tokens;
	TArray<FString> Switches;
	TMap<FString, FString> Params;
	ParseCommandLine(*CommandLine, Tokens, Switches, Params);

	FString OutputFile;
	FParse::Value(*CommandLine, TEXT("outfile="), OutputFile);

	if (OutputFile.IsEmpty())
	{
		UE_LOG(LogBlueprintInspector, Error, TEXT("You MUST specify the output json file using the -outfile= argument."));
		return -1;
	}

	if (Switches.Contains(TEXT("SkipEngine")))
	{
		bSkipEngine = true;
	}

	if (Switches.Contains(TEXT("SkipPlugins")))
	{
		bSkipPlugins = true;
	}

	if (Switches.Contains(TEXT("SkipDevelopers")))
	{
		bSkipDevelopers = true;
	}

	if (Switches.Contains(TEXT("SkipGame")))
	{
		bSkipGame = true;
	}

	FAssetRegistryModule& AssetRegistryModule = FModuleManager::LoadModuleChecked<FAssetRegistryModule>(TEXT("AssetRegistry"));
	AssetRegistryModule.Get().SearchAllAssets(/*bSynchronousSearch =*/true);

	FARFilter Filter;

#if ((ENGINE_MAJOR_VERSION == 4) && (ENGINE_MINOR_VERSION == 27)) || ((ENGINE_MAJOR_VERSION == 5) && (ENGINE_MINOR_VERSION == 0))
	TArrayView<const FName> BlueprintClassNames = FBlueprintAssetHandler::Get().GetRegisteredClassNames();
#else
	TArrayView<const FTopLevelAssetPath, int32> BlueprintClassNames = FBlueprintAssetHandler::Get().GetRegisteredClassNames();
#endif

	for (int index = 0; index < BlueprintClassNames.Num(); ++index)
	{
#if ((ENGINE_MAJOR_VERSION == 4) && (ENGINE_MINOR_VERSION == 27)) || ((ENGINE_MAJOR_VERSION == 5) && (ENGINE_MINOR_VERSION == 0))
		Filter.ClassNames.Add(BlueprintClassNames[index]);
#else
		Filter.ClassPaths.Add(BlueprintClassNames[index]);
#endif
	}
	Filter.bRecursiveClasses = true;

	// filter the asset registry populate to make it quicker when testing specific packages
	//Filter.PackagePaths.Add("/Game/Developers/SomeUser/Maps");
	//Filter.bRecursivePaths = true;

	TArray<FAssetData> BlueprintAssets;  // this is the list of all Blueprint assets
	AssetRegistryModule.Get().GetAssets(Filter, BlueprintAssets);

	TArray<BlueprintClassInfo> BlueprintClassInfos;
	TMap<UClass*, int32> FunctionClassToClassesIndex;

	// first, iterate through all classes to gather any native Blueprint function names (there's a LOT of these)
	for (TObjectIterator<UClass> ClassIt; ClassIt; ++ClassIt)
	{
		for (TFieldIterator<UFunction> FuncIter(*ClassIt, EFieldIteratorFlags::ExcludeSuper); FuncIter; ++FuncIter)
		{
			if ((*FuncIter)->HasAnyFunctionFlags(FUNC_Native) && (*FuncIter)->HasAnyFunctionFlags(FUNC_BlueprintCallable | FUNC_BlueprintEvent | FUNC_BlueprintPure | FUNC_BlueprintAuthorityOnly | FUNC_BlueprintCosmetic))
			{
				if (FuncIter == nullptr)
				{
					continue;  // should never happen
				}

				FString FunctionName = (*FuncIter)->GetName();

				// see if we have already added this class...
				int32* Class_ptr = FunctionClassToClassesIndex.Find(*ClassIt);

				if (Class_ptr)  // does the class already exist?
				{
					int32 ClassIndex = *Class_ptr;

					// see if we have already added this function name (should never happen)...
					int32* Info_ptr = BlueprintClassInfos[ClassIndex].FunctionNameToInfoIndex.Find(FunctionName);

					if (Info_ptr)
					{
						ensure(true);  // this should never happen
					}
					else  // add the new function name to the class
					{
						BlueprintFunctionInfo Info;
						Info.FunctionName = FunctionName;

						int32 NewFunctionIndex = BlueprintClassInfos[ClassIndex].FunctionInfos.Num();

						BlueprintClassInfos[ClassIndex].FunctionInfos.Add(Info);
						BlueprintClassInfos[ClassIndex].FunctionNameToInfoIndex.Add(FunctionName, NewFunctionIndex);
					}
				}
				else  // this is a new class, add it and this function name
				{
					int32 NewClassIndex = BlueprintClassInfos.Num();
					FunctionClassToClassesIndex.Add((*ClassIt), NewClassIndex);

					BlueprintFunctionInfo Info;
					Info.FunctionName = FunctionName;

					BlueprintClassInfo NewClass;
					NewClass.BlueprintClass = (*ClassIt);
					NewClass.ClassName = FString::Printf(TEXT("%s%s"), (*ClassIt)->GetPrefixCPP(), *(*ClassIt)->GetName());

					NewClass.FunctionInfos.Add(Info);
					NewClass.FunctionNameToInfoIndex.Add(FunctionName, 0);

					BlueprintClassInfos.Add(NewClass);
				}
			}
		}
	}

	int32 TotalBlueprintNativeFunctions = 0;

	// next, iterate through all of the Blueprint assets to find Blueprint functions that are in use
	for (const FAssetData Asset : BlueprintAssets)
	{
		UClass* AssetClass = Asset.GetClass();

		if (AssetClass == nullptr)
		{
			continue;
		}

		FString BlueprintAssetPathName = Asset.PackageName.ToString();

		if (bSkipEngine && BlueprintAssetPathName.StartsWith("/Engine/"))
		{
			continue;
		}

		// plugins will be something that doesn't start with "/Engine/" or "/Game/"
		if (bSkipPlugins && (!BlueprintAssetPathName.StartsWith("/Engine/") && !BlueprintAssetPathName.StartsWith("/Game/")))
		{
			continue;
		}

		if (bSkipDevelopers && BlueprintAssetPathName.Contains("/Developers/"))
		{
			continue;
		}

		if (bSkipGame && BlueprintAssetPathName.StartsWith("/Game/"))
		{
			continue;
		}

		UObject* AssetObject = Asset.GetAsset();  // this can call LoadObject if the object isn't already loaded (slow)

		if (AssetObject == nullptr)
		{
			continue;
		}

		const IBlueprintAssetHandler* Handler = FBlueprintAssetHandler::Get().FindHandler(AssetClass);
		UBlueprint* Blueprint = Handler ? Handler->RetrieveBlueprint(AssetObject) : nullptr;
		if (Blueprint == nullptr)  // skip any Blueprint asset without a handler (should never happen)
		{
			continue;
		}

		UBlueprintGeneratedClass* BlueprintGeneratedClass = Cast<UBlueprintGeneratedClass>(Blueprint->GeneratedClass);
		if (BlueprintGeneratedClass == nullptr)  // skip any Blueprints without a BPGC (should only happen if a Blueprint was saved without being compiled, but should be compiled on load anyway)
		{
			continue;
		}

		TArray<UK2Node*> K2Nodes;
		FBlueprintInspectorModule::GatherK2NodesForAsset(Blueprint, RF_Transient, K2Nodes);  // RF_Transient for nodes loaded by the commandlet and RF_Transactional for nodes loaded by the Blueprint editor

		int32 BlueprintAssetIndex = INDEX_NONE;

		for (int32 node_index = 0; node_index < K2Nodes.Num(); ++node_index)
		{
			UClass* NodeClass = nullptr;
			FName NodeFunction = NAME_None;
			FBlueprintInspectorModule::GetK2NodeClassFunction(BlueprintGeneratedClass, K2Nodes[node_index], NodeClass, NodeFunction);

#if 0  // There's a lot of these.  This will happen for internal Blueprint functions (functions created within the Blueprint itself and called by that Blueprint)
			if (NodeClass == nullptr)
			{
				UE_LOG(LogBlueprintInspector, Log, TEXT("NodeClass = nullptr for asset = %s, index %d, K2Node '%s', function %s"), *BlueprintAssetPathName, node_index, *K2Nodes[node_index]->GetName(), *NodeFunction.ToString());
			}
#endif
			if ((NodeClass != nullptr) && (NodeFunction != NAME_None))
			{
				int32 ClassIndex = INDEX_NONE;
				int32 FuncIndex = INDEX_NONE;

				bool bDone = false;

				while (!bDone)
				{
					int32* Class_ptr = FunctionClassToClassesIndex.Find(NodeClass);  // find the class
					if (Class_ptr)
					{
						ClassIndex = *Class_ptr;

						int32* Func_ptr = BlueprintClassInfos[ClassIndex].FunctionNameToInfoIndex.Find(NodeFunction.ToString());  // find the function
						if (Func_ptr)
						{
							FuncIndex = *Func_ptr;
							break;
						}
					}

					NodeClass = NodeClass->GetSuperClass();
					if (NodeClass == nullptr)
					{
						break;
					}
				}

				if ((ClassIndex != INDEX_NONE) && (FuncIndex != INDEX_NONE))
				{
					if (BlueprintAssetIndex == INDEX_NONE)
					{
						BlueprintAssetIndex = BlueprintAssetPathNames.Find(BlueprintAssetPathName);
					}

					if (BlueprintAssetIndex == INDEX_NONE)
					{
						BlueprintAssetIndex = BlueprintAssetPathNames.Num();
						BlueprintAssetPathNames.Add(BlueprintAssetPathName);
					}

					bool bFound = false;
					for( int32 index = 0; index < BlueprintClassInfos[ClassIndex].FunctionInfos[FuncIndex].BlueprintAssetIndexInfos.Num(); ++index)
					{
						if (BlueprintClassInfos[ClassIndex].FunctionInfos[FuncIndex].BlueprintAssetIndexInfos[index].BlueprintAssetIndex == BlueprintAssetIndex)
						{
							bFound = true;
							BlueprintClassInfos[ClassIndex].FunctionInfos[FuncIndex].BlueprintAssetIndexInfos[index].Count++;

							TotalBlueprintNativeFunctions++;

							break;
						}
					}

					if (!bFound)
					{
						// add the asset path index to the function list for this class
						BlueprintAssetIndexInfo BPAssetIndex;
						BPAssetIndex.BlueprintAssetIndex = BlueprintAssetIndex;
						BPAssetIndex.Count = 1;

						BlueprintClassInfos[ClassIndex].FunctionInfos[FuncIndex].BlueprintAssetIndexInfos.Add(BPAssetIndex);

						TotalBlueprintNativeFunctions++;
					}
				}
			}
		}

		UPackage* BlueprintPackage = Blueprint->GetPackage();

		if ((++GCCounter % 100 == 0) || (BlueprintPackage && BlueprintPackage->ContainsMap()))
		{
			CollectGarbage(RF_NoFlags);
		}
	}

	// sort the class names...
	BlueprintClassInfos.Sort([](const BlueprintClassInfo& A, const BlueprintClassInfo& B)
		{
			return A.ClassName < B.ClassName;
		});

	// sort the functions names in each class (this will screw up the 'FunctionNameToInfoIndex', but we don't care at this point)
	for (int32 index = 0; index < BlueprintClassInfos.Num(); ++index)
	{
		// sort the functions
		BlueprintClassInfos[index].FunctionInfos.Sort([](const BlueprintFunctionInfo& A, const BlueprintFunctionInfo& B)
			{
				return A.FunctionName < B.FunctionName;
			});

		BlueprintClassInfos[index].FunctionNameToInfoIndex.Empty();
	}

	// then, output the .json file with the gathered class/function information
	FString OutputString;

//	TSharedRef<TJsonWriter<TCHAR, TPrettyJsonPrintPolicy<TCHAR>>> JsonWriter = TJsonWriterFactory<TCHAR, TPrettyJsonPrintPolicy<TCHAR>>::Create(&OutputString);
	TSharedRef<TJsonWriter<TCHAR, TCondensedJsonPrintPolicy<TCHAR>>> JsonWriter = TJsonWriterFactory<TCHAR, TCondensedJsonPrintPolicy<TCHAR>>::Create(&OutputString);

	JsonWriter->WriteObjectStart();

	JsonWriter->WriteIdentifierPrefix(TEXT("version"));
	JsonWriter->WriteObjectStart();
	JsonWriter->WriteValue(TEXT("version"), TEXT("1"));  // this will be handy if we change the json format later
	JsonWriter->WriteObjectEnd();

	JsonWriter->WriteIdentifierPrefix(TEXT("blueprints"));
	JsonWriter->WriteArrayStart();
	for (int32 BlueprintIndex = 0; BlueprintIndex < BlueprintAssetPathNames.Num(); ++BlueprintIndex)
	{
		JsonWriter->WriteObjectStart();
		JsonWriter->WriteValue(TEXT("bpasset"), FString::Printf(TEXT("%s"), *BlueprintAssetPathNames[BlueprintIndex]));
		JsonWriter->WriteObjectEnd();
	}
	JsonWriter->WriteArrayEnd();

	JsonWriter->WriteIdentifierPrefix(TEXT("classes"));
	JsonWriter->WriteArrayStart();
	for (int32 ClassIndex = 0; ClassIndex < BlueprintClassInfos.Num(); ++ClassIndex)
	{
		JsonWriter->WriteObjectStart();
		JsonWriter->WriteValue(TEXT("class"), FString::Printf(TEXT("%s"), *BlueprintClassInfos[ClassIndex].ClassName.ToLower()));

		JsonWriter->WriteIdentifierPrefix(TEXT("functions"));
		JsonWriter->WriteArrayStart();
		for (int32 FunctionIndex = 0; FunctionIndex < BlueprintClassInfos[ClassIndex].FunctionInfos.Num(); ++FunctionIndex)
		{
			JsonWriter->WriteObjectStart();
			JsonWriter->WriteValue(TEXT("func"), FString::Printf(TEXT("%s"), *BlueprintClassInfos[ClassIndex].FunctionInfos[FunctionIndex].FunctionName.ToLower()));

			JsonWriter->WriteIdentifierPrefix(TEXT("bps"));
			JsonWriter->WriteArrayStart();
			for (int32 BlueprintIndex = 0; BlueprintIndex < BlueprintClassInfos[ClassIndex].FunctionInfos[FunctionIndex].BlueprintAssetIndexInfos.Num(); ++BlueprintIndex)
			{
				JsonWriter->WriteObjectStart();
				JsonWriter->WriteValue(TEXT("bpasset"), FString::Printf(TEXT("%d"), BlueprintClassInfos[ClassIndex].FunctionInfos[FunctionIndex].BlueprintAssetIndexInfos[BlueprintIndex].BlueprintAssetIndex));
				JsonWriter->WriteValue(TEXT("count"), FString::Printf(TEXT("%d"), BlueprintClassInfos[ClassIndex].FunctionInfos[FunctionIndex].BlueprintAssetIndexInfos[BlueprintIndex].Count));
				JsonWriter->WriteObjectEnd();
			}
			JsonWriter->WriteArrayEnd();

			JsonWriter->WriteObjectEnd();
		}
		JsonWriter->WriteArrayEnd();

		JsonWriter->WriteObjectEnd();
	}
	JsonWriter->WriteArrayEnd();

	JsonWriter->WriteObjectEnd();
	JsonWriter->Close();

	FFileHelper::SaveStringToFile(OutputString, *OutputFile);

	UE_LOG(LogBlueprintInspector, Log, TEXT("Found %i total native Blueprint functions"), TotalBlueprintNativeFunctions);

	UE_LOG(LogBlueprintInspector, Log, TEXT("Successfully finished running BlueprintInspector Commandlet"));
	UE_LOG(LogBlueprintInspector, Log, TEXT("--------------------------------------------------------------------------------"));

	return 0;
}
