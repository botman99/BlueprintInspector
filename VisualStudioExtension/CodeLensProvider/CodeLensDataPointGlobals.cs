using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeLensProvider
{
	public static class CodeLensDataPointGlobals
	{
		public static volatile bool bNeedToInitializeSolutionDirectory = true;
		public static string SolutionDirectory = "";  // ends with trailing "\\"

		public static List<CodeLensDataPoint> CodeLensDataPoints = null;

		public static ushort JsonFileUpdateCounter = 0;
		public static ushort JsonFileUpdatePreviousCounter = 0;
		public static volatile bool bNeedsToReadJsonFile = true;
		internal static volatile SharedProject.BlueprintJson BlueprintJsonData = null;

		public static Object BlueprintAssetPathLock = new object();
		public static volatile bool bHasBlueprintAssetPathToSend = false;
		public static volatile List<string> BlueprintAssetPathToSend;

		public static Int32 EditorTopLevelWindowHandle = -1;
	}
}
