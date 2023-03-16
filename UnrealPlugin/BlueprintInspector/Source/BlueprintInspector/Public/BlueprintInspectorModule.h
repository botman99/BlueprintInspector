// Copyright 2023 - Jeffrey "botman" Broome. All Rights Reserved

#pragma once

#include "BlueprintInspector.h"

#include "CoreMinimal.h"
#include "Modules/ModuleInterface.h"
#include "Modules/ModuleManager.h"
#include "Containers/Ticker.h"
#include "Async/Future.h"
#include "HAL/PlatformNamedPipe.h"

class FBlueprintInspectorModule : public IModuleInterface
{
public:
	/** IModuleInterface implementation */
	virtual void StartupModule() override;
	virtual void ShutdownModule() override;

#if WITH_EDITOR
	static void GatherK2NodesForAsset(UObject* BlueprintAsset, EObjectFlags ObjectFlag, TArray<class UK2Node*>& K2Nodes);
	static void GetK2NodeClassFunction(class UBlueprintGeneratedClass* BlueprintGeneratedClass, class UK2Node* K2Node, UClass*& NodeClass, FName& NodeFunction);

	void OnAssetRegistryFilesLoaded();

#if (ENGINE_MAJOR_VERSION == 4) && (ENGINE_MINOR_VERSION == 27)
	FDelegateHandle TickerHandle;
#else
	FTSTicker::FDelegateHandle TickerHandle;
#endif

	bool CheckForAssetToOpen(float FrameDeltaTime);

	bool bIsAssetRegistryLoaded = false;

	FPlatformNamedPipe BlueprintInspectorNamedPipe;
	TFuture<void> ReadPipeThreadFuture;
	static bool bCanReadFromPipe;

	void ReadPipeThreadProc();

	static TArray<FName> ActorFunctions;  // list of Blueprint functions in AActor
	static TMap<FName, UClass*> ClassNameToClassPointer;

	static FCriticalSection BlueprintInspectorMutex;  // the mutex we use to copy the blueprint asset data to the game thread
	static TArray<FString> AssetPaths;
	static bool bAssetsReadyToProcess;  // are asset paths ready to process? (i.e. is AssetPaths array not empty)

	static UObject* BlueprintObject;  // the asset we just opened in the Blueprint editor
	static UClass* BlueprintSearchClass;
	static FName BlueprintSearchFunction;
	static bool bNeedToSearchForFunction;  // set after we open an asset in the Blueprint editor indicating that we need to search for the K2 node

#endif
};
