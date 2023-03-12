// Copyright Jeffrey "botman" Broome. All Rights Reserved

using UnrealBuildTool;

public class BlueprintInspector : ModuleRules
{
	public BlueprintInspector(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(
			new[]
			{
				"Core",
			}
		);

		PrivateDependencyModuleNames.AddRange(
			new string[]
			{
				"CoreUObject",
				"AssetRegistry",
				"Engine",
				"UnrealEd",
				"BlueprintGraph",
				"Kismet",
				"Projects",
				"Json",
			}
		);

		PublicIncludePaths.Add(ModuleDirectory + "/Public");

		int EngineMajor = 0;
		int EngineMinor = 0;

		GetEngineVersion(ref EngineMajor, ref EngineMinor);

		PrivateDefinitions.Add(string.Format("ENGINE_MAJOR_VERSION={0}", EngineMajor));
		PrivateDefinitions.Add(string.Format("ENGINE_MINOR_VERSION={0}", EngineMinor));
	}

	private void GetEngineVersion(ref int EngineMajor, ref int EngineMinor)
	{
		EngineMajor = 0;
		EngineMinor = 0;

		string input;
		using(System.IO.StreamReader sr = new System.IO.StreamReader("Runtime/Launch/Resources/Version.h"))
		{
			while ((input = sr.ReadLine()) != null)
			{
				if (input.StartsWith("#define ENGINE_MAJOR_VERSION"))
				{
					string[] fields = input.Split();
					if (fields.Length > 2)
					{
						EngineMajor = int.Parse(fields[2]);
					}
				}
				else if (input.StartsWith("#define ENGINE_MINOR_VERSION"))
				{
					string[] fields = input.Split();
					if (fields.Length > 2)
					{
						EngineMinor = int.Parse(fields[2]);
					}
				}
			}
		}
	}
}
