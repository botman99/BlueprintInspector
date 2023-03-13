// Copyright 2023 - Jeffrey "botman" Broome. All Rights Reserved

#pragma once

#include "Commandlets/Commandlet.h"
#include "UObject/Interface.h"
#include "BlueprintInspectorCommandlet.generated.h"

UCLASS(CustomConstructor)
class BLUEPRINTINSPECTOR_API UBlueprintInspectorCommandlet : public UCommandlet
{
	GENERATED_UCLASS_BODY()

public:
	UBlueprintInspectorCommandlet(const FObjectInitializer& ObjectInitializer)
		: Super(ObjectInitializer)
	{
		LogToConsole = false;
	}

	// Begin UCommandlet Interface
	virtual int32 Main(const FString& Params) override;
	// End UCommandlet Interface
};
