using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using Microsoft.VisualStudio.Utilities;
using CodeLensProvider.Properties;
using Microsoft.VisualStudio.Language.CodeLens;
using System.Diagnostics;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.Timers;

namespace CodeLensProvider
{
	[Export(typeof(IAsyncCodeLensDataPointProvider))]
	[Name(Id)]
//	[ContentType("code")]
	[ContentType("C/C++")]
//	[ContentType("CppProperties")]
	[LocalizedName(typeof(Resources), "BlueprintInspector")]
	[Priority(200)]
    public class CodeLensDataPointProvider : IAsyncCodeLensDataPointProvider
    {
		internal const string Id = "BlueprintInspector";

		private static Int32 VisualStudioProcessId = 0;

		private static MemoryMappedFile mmf = null;
		private static Mutex mutex = null;
		private static System.Timers.Timer timer;


		public CodeLensDataPointProvider()
		{
			try
			{
				int count = 10;
				Process proc = Process.GetCurrentProcess();

				while ((proc != null) && (proc.ProcessName != "devenv") && (count != 0))
				{
					proc = SharedProject.SharedGlobals.ParentProcessUtilities.GetParentProcess(proc.Id);
					count--;
				}

				if (proc.ProcessName == "devenv")
				{
					VisualStudioProcessId = proc.Id;
				}

#if OutputDebugString
				if (SharedProject.SharedGlobals.debug_mutex == null)
				{
					SharedProject.SharedGlobals.debug_mutex = Mutex.OpenExisting(String.Format("BlueprintInspector_debugmutex{0}", VisualStudioProcessId));
				}

				if (SharedProject.SharedGlobals.debug_mutex != null)
				{
					SharedProject.SharedGlobals.OutputDebugString("CodeLensDataPointProvider() - debug_mutex initialized");
					SharedProject.SharedGlobals.OutputDebugString(String.Format("CodeLensDataPointProvider() - VisualStudioProcessId = {0}", VisualStudioProcessId));
				}
#endif

				if (mutex == null)
				{
					mutex = Mutex.OpenExisting(String.Format("BlueprintInspector_mutex{0}", VisualStudioProcessId));
				}

				if (mmf == null)
				{
					mmf = MemoryMappedFile.OpenExisting(String.Format("BlueprintInspector{0}", VisualStudioProcessId));
				}

				if (mmf != null && mutex != null)
				{
					mutex.WaitOne();

					using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, 4096))
					{
						CodeLensDataPointGlobals.JsonFileUpdateCounter = accessor.ReadUInt16(0);

						ushort Size = accessor.ReadUInt16(2);
						byte[] Buffer = new byte[Size];
						accessor.ReadArray(4, Buffer, 0, Buffer.Length);

						CodeLensDataPointGlobals.SolutionDirectory = ASCIIEncoding.ASCII.GetString(Buffer);

						CodeLensDataPointGlobals.bNeedToInitializeSolutionDirectory = false;
					}

					// keep track of the value of the counter the last time we read it (so we can tell when it changes)
					CodeLensDataPointGlobals.JsonFileUpdatePreviousCounter = CodeLensDataPointGlobals.JsonFileUpdateCounter;

					mutex.ReleaseMutex();
				}

				if (CodeLensDataPointGlobals.bNeedsToReadJsonFile && CodeLensDataPointGlobals.SolutionDirectory != "")
				{
					string filename = CodeLensDataPointGlobals.SolutionDirectory + ".vs\\BlueprintInspector.json";

					if (File.Exists(filename))
					{
						CodeLensDataPointGlobals.BlueprintJsonData = new SharedProject.BlueprintJson();
						CodeLensDataPointGlobals.BlueprintJsonData.ReadBlueprintJson(filename);
					}

#if OutputDebugString
					SharedProject.SharedGlobals.OutputDebugString(String.Format("CodeLensDataPointProvider() - BlueprintJsonData.classes.Count() = {0}", CodeLensDataPointGlobals.BlueprintJsonData.classes.Count));
#endif

					CodeLensDataPointGlobals.bNeedsToReadJsonFile = false;
				}

				timer = new System.Timers.Timer();
				timer.Elapsed += new ElapsedEventHandler(OnTimerEvent);
				timer.AutoReset = true;
				timer.Enabled = true;
				timer.Interval = 500;
			}
			catch(Exception)
			{
			}
		}

		public Task<bool> CanCreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			if (CodeLensDataPointGlobals.bNeedToInitializeSolutionDirectory)
			{
				// shouldn't be here
				return Task.FromResult<bool>(false);
			}

			if (CodeLensDataPointGlobals.bNeedsToReadJsonFile && CodeLensDataPointGlobals.SolutionDirectory != "")
			{
				string filename = CodeLensDataPointGlobals.SolutionDirectory + ".vs\\BlueprintInspector.json";

				if (File.Exists(filename))
				{
					CodeLensDataPointGlobals.BlueprintJsonData = new SharedProject.BlueprintJson();
					CodeLensDataPointGlobals.BlueprintJsonData.ReadBlueprintJson(filename);
				}

				CodeLensDataPointGlobals.bNeedsToReadJsonFile = false;
			}

			bool bHasValidBlueprintFunctionName = false;

			if (CodeLensDataPointGlobals.BlueprintJsonData != null)
			{
				string FullyQualifiedName = "";

				try
				{
					foreach(object SomeObj in descriptorContext.Properties)
					{
						if (SomeObj != null)
						{
							string SomeObjString = SomeObj.ToString();
							if (SomeObjString.Contains("FullyQualifiedName"))
							{
								FullyQualifiedName = SomeObjString.Replace("[", "").Replace("]", "");
								FullyQualifiedName = FullyQualifiedName.Replace("FullyQualifiedName, ", "");
							}
						}
					}

					if (FullyQualifiedName != "" && FullyQualifiedName.Contains("::"))
					{
						string class_name = "";
						string function_name = "";

						int index = FullyQualifiedName.IndexOf("::");
						if (index > 0 && (index < FullyQualifiedName.Length - 2))
						{
							class_name = FullyQualifiedName.Substring(0, index);
							function_name = FullyQualifiedName.Substring(index + 2);
						}

						if (CodeLensDataPointGlobals.BlueprintJsonData != null && class_name != "" && function_name != "")
						{
							int class_index = CodeLensDataPointGlobals.BlueprintJsonData.GetClassIndex(class_name);

							if (class_index != -1)
							{
								int function_index = CodeLensDataPointGlobals.BlueprintJsonData.GetFunctionIndex(class_index, function_name);

								if (function_index != -1)
								{
									bHasValidBlueprintFunctionName = true;
								}
							}
						}
					}
				}
				catch(Exception)
				{
				}
			}

			return Task.FromResult<bool>(bHasValidBlueprintFunctionName);
		}

		public Task<IAsyncCodeLensDataPoint> CreateDataPointAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext descriptorContext, CancellationToken token)
		{
			string FullyQualifiedName = "";
			string class_name = "";
			string function_name = "";

			if (CodeLensDataPointGlobals.BlueprintJsonData != null)
			{
				try
				{
					foreach(object SomeObj in descriptorContext.Properties)
					{
						if (SomeObj != null)
						{
							string SomeObjString = SomeObj.ToString();
							if (SomeObjString.Contains("FullyQualifiedName"))
							{
								FullyQualifiedName = SomeObjString.Replace("[", "").Replace("]", "");
								FullyQualifiedName = FullyQualifiedName.Replace("FullyQualifiedName, ", "");
							}
						}
					}
				}
				catch(Exception)
				{
				}

				if (FullyQualifiedName != "" && FullyQualifiedName.Contains("::"))
				{
					int index = FullyQualifiedName.IndexOf("::");
					if (index > 0 && (index < FullyQualifiedName.Length - 2))
					{
						class_name = FullyQualifiedName.Substring(0, index);
						function_name = FullyQualifiedName.Substring(index + 2);
					}
				}
			}

			CodeLensDataPoint NewCodeLensDataPoint = new CodeLensDataPoint(descriptor, FullyQualifiedName, class_name, function_name);

			try
			{
				if (CodeLensDataPointGlobals.CodeLensDataPoints == null)
				{
					CodeLensDataPointGlobals.CodeLensDataPoints = new List<CodeLensDataPoint>();
				}

				CodeLensDataPointGlobals.CodeLensDataPoints.Add(NewCodeLensDataPoint);
			}
			catch(Exception)
			{
			}

			return Task.FromResult<IAsyncCodeLensDataPoint>(NewCodeLensDataPoint);
		}

		private static void OnTimerEvent(object source, ElapsedEventArgs e)
		{
			try
			{
				// update the globals memory (in the event that things have changed)
				if (mmf != null && mutex != null)
				{
					mutex.WaitOne();

					using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, 4096))
					{
						CodeLensDataPointGlobals.JsonFileUpdateCounter = accessor.ReadUInt16(0);

						ushort Size = accessor.ReadUInt16(2);
						byte[] Buffer = new byte[Size];
						accessor.ReadArray(4, Buffer, 0, Buffer.Length);

						CodeLensDataPointGlobals.SolutionDirectory = ASCIIEncoding.ASCII.GetString(Buffer);
					}

					mutex.ReleaseMutex();
				}

				// has the json file been updated?
				if (CodeLensDataPointGlobals.JsonFileUpdatePreviousCounter != CodeLensDataPointGlobals.JsonFileUpdateCounter)
				{
					// keep track of the value of the counter the last time we read it (so we can tell when it changes)
					CodeLensDataPointGlobals.JsonFileUpdatePreviousCounter = CodeLensDataPointGlobals.JsonFileUpdateCounter;

					CodeLensDataPointGlobals.bNeedsToReadJsonFile = true;

					// We need to invalidate all CodeLensDataPoint objects here.  Call the Invalidate() method on them.
					for (Int32 index = 0; index < CodeLensDataPointGlobals.CodeLensDataPoints.Count; ++index)
					{
						if (CodeLensDataPointGlobals.CodeLensDataPoints[index] != null)
						{
							CodeLensDataPointGlobals.CodeLensDataPoints[index].blueprint_asset_list = null;
							CodeLensDataPointGlobals.CodeLensDataPoints[index].Invalidate();
						}
					}
				}
			}
			catch(Exception)
			{
			}
		}
    }
}
