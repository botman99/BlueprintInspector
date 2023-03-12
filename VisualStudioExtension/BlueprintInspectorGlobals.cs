using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;

namespace BlueprintInspector
{
	public static class BlueprintInspectorGlobals
	{
		public static string SolutionDirectory = "";  // ends with trailing "\\"
		public static string EngineDirectory = "";  // without the trailing "\\"
		public static string ProjectFilename = "";

		public static ushort JsonFileUpdateCounter = 0;  // increment this counter each time the JSON file is rebuilt (this will get sent to the provider via shared memory)

		public static Object BlueprintAssetPathLock = new object();
		public static volatile bool bHasBlueprintAssetPathToSend = false;
		public static volatile List<string> BlueprintAssetPathToSend = new List<string>();

		public static AsyncPackage package = null;

		public static IVsOutputWindowPane OutputPane;

		public static Int32 EditorTopLevelWindowHandle = -1;
	}
}
