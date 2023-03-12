using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO.MemoryMappedFiles;

namespace BlueprintInspector
{
	public class RunExecutable
	{
		private bool StdOutDone;  // wait until OnOutputDataReceived receives e.Data == null to know that stdout has terminated
		private bool StdErrDone;  // wait until OnErrorDataReceived receives e.Data == null to know that stderr has terminated

		private bool bIsUE4Project;
		private bool bIncludeEngine;
		private bool bIncludePlugins;
		private bool bIncludeDevelopers;

		private MemoryMappedFile mmf = null;
		private Mutex mutex = null;

		private bool JsonFileUpdated = false;
		private bool bRestartVisualStudio = false;
		private SharedProject.BlueprintJson OldBlueprintJsonData = null;
		private SharedProject.BlueprintJson NewBlueprintJsonData = null;

		private bool bBlueprintInspectorCommandletStarted = false;
		private bool bBlueprintInspectorCommandletCompleted = false;
		private bool bCommandletProcessCompleted = false;

		public RunExecutable(bool bInIsUE4Project, bool bInIncludeEngine, bool bInIncludePlugins, bool bInIncludeDevelopers)
		{
			bIsUE4Project = bInIsUE4Project;
			bIncludeEngine = bInIncludeEngine;
			bIncludePlugins = bInIncludePlugins;
			bIncludeDevelopers = bInIncludeDevelopers;
		}

		public void Run()
		{
			try
			{
				string json_filename = BlueprintInspectorGlobals.SolutionDirectory + ".vs\\BlueprintInspector.json";

				if (File.Exists(json_filename))
				{
					OldBlueprintJsonData = new SharedProject.BlueprintJson();
					OldBlueprintJsonData.ReadBlueprintJson(json_filename);
				}
				else
				{
					// if the json file doesn't already exist before running the commandlet, we should ALWAYS restart Visual Studio after it's generated
					bRestartVisualStudio = true;
				}

				// use EngineDirectory, ProjectFilename and SolutionDirectory in BlueprintInspectorGlobals to run the commandlet...
				string UnrealEditorCmd = "";

				if (bIsUE4Project)
				{
					UnrealEditorCmd = "\"" + BlueprintInspectorGlobals.EngineDirectory + "\\Binaries\\Win64\\UE4Editor-Cmd.exe\"";
				}
				else
				{
					UnrealEditorCmd = "\"" + BlueprintInspectorGlobals.EngineDirectory + "\\Binaries\\Win64\\UnrealEditor-Cmd.exe\"";
				}

				string command = String.Format("\"{0}\" -Log -FullStdOutLogOutput -run=BlueprintInspectorCommandlet -outfile=\"{1}\"", BlueprintInspectorGlobals.ProjectFilename, json_filename);

				if (!bIncludeEngine)
				{
					command += " -skipengine";
				}

				if (!bIncludePlugins)
				{
					command += " -skipplugins";
				}

				if (!bIncludeDevelopers)
				{
					command += " -skipdevelopers";
				}

				Process proc = new Process();

				StdOutDone = false;
				StdErrDone = false;

				ProcessStartInfo startInfo = new ProcessStartInfo(UnrealEditorCmd);

				startInfo.UseShellExecute = false;
				startInfo.CreateNoWindow = true;

				startInfo.RedirectStandardInput = false;
				startInfo.RedirectStandardOutput = true;
				startInfo.RedirectStandardError = true;

				startInfo.Arguments = command;

				proc.StartInfo = startInfo;
				proc.EnableRaisingEvents = true;
//				proc.Exited += OnProcessExited;

				proc.OutputDataReceived += OnOutputDataReceived;
				proc.ErrorDataReceived += OnErrorDataReceived;

				proc.Start();
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();

				if (!proc.WaitForExit(-1))
				{
					proc.Kill();
				}

				int output_timeout = 10;
				while (!StdOutDone || !StdErrDone)  // wait until the output and error streams have been flushed (or a 1 second (1000 ms) timeout is reached)
				{
					Thread.Sleep(100);

					if (--output_timeout == 0)
					{
						break;
					}
				}

				proc.Close();

				if (File.Exists(json_filename))
				{
					// check if the date of the json file changed and is very recent (to verify that the commandlet updated the file)
					DateTime filetime = File.GetLastWriteTime(json_filename);
					double deltatime = DateTime.Now.Subtract(filetime).TotalSeconds;
					if (deltatime < 300)  // less than 5 minutes ago?
					{
						JsonFileUpdated = true;

						BlueprintInspectorGlobals.JsonFileUpdateCounter++;

						Common common = new Common();
						common.WriteSharedMemoryData(mmf, mutex);

						NewBlueprintJsonData = new SharedProject.BlueprintJson();
						NewBlueprintJsonData.ReadBlueprintJson(json_filename);

						// compare the old json data to the new json data to see if they match or not (if not, restart of Visual Studio is required)
						if (OldBlueprintJsonData != null)
						{
							if (!OldBlueprintJsonData.Compare(NewBlueprintJsonData))
							{
								bRestartVisualStudio = true;
							}
						}
					}
				}

				_ = BlueprintInspectorGlobals.package.JoinableTaskFactory.RunAsync(() => DisplayCommandletCompleteMessageAsync());

			}
			catch (Exception)
			{
			}
		}

		private async System.Threading.Tasks.Task AddOutputStringAsync(string msg)
		{
			if (bCommandletProcessCompleted)
			{
				return;
			}

			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(BlueprintInspectorGlobals.package.DisposalToken);

			if (msg.Contains("LogBlueprintInspector: Running BlueprintInspector Commandlet"))
			{
				bBlueprintInspectorCommandletStarted = true;
			}

			if (msg.Contains("LogBlueprintInspector: Successfully finished running BlueprintInspector Commandlet"))
			{
				bBlueprintInspectorCommandletCompleted = true;
			}

			if (msg.Contains("Execution of commandlet took:"))  // ignore any further output after this text is seen
			{
				bCommandletProcessCompleted = true;
			}

			BlueprintInspectorGlobals.OutputPane.OutputStringThreadSafe(msg);
			BlueprintInspectorGlobals.OutputPane.OutputStringThreadSafe("\n");
		}

		private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				_ = BlueprintInspectorGlobals.package.JoinableTaskFactory.RunAsync(() => AddOutputStringAsync(e.Data));
			}
			else
			{
				StdOutDone = true;
			}
		}

		private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				_ = BlueprintInspectorGlobals.package.JoinableTaskFactory.RunAsync(() => AddOutputStringAsync(e.Data));
			}
			else
			{
				StdErrDone = true;
			}
		}

//		public void OnProcessExited(object sender, EventArgs e)
//		{
//		}

		private async System.Threading.Tasks.Task DisplayCommandletCompleteMessageAsync()
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(BlueprintInspectorGlobals.package.DisposalToken);

			string title = "";
			string message = "";

			OLEMSGICON icon = OLEMSGICON.OLEMSGICON_INFO;

			if (!bBlueprintInspectorCommandletStarted)
			{
				title = "Failed";
				message = "BlueprintInspector commandlet didn't start running.\n\nMake sure you have added the 'Blueprint Inspector' plugin to your project in the editor (Edit -> Plugins).";
				icon = OLEMSGICON.OLEMSGICON_CRITICAL;
			}
			else if (!bBlueprintInspectorCommandletCompleted)
			{
				title = "Failed";
				message = "BlueprintInspector commandlet didn't finish running.\n\nCheck your project's Saved\\Logs folder for the most recent .log file and open it in a text editor to see if the process crashed.";
				icon = OLEMSGICON.OLEMSGICON_CRITICAL;
			}
			else if (!JsonFileUpdated)
			{
				title = "Failed";
				message = "BlueprintInspector commandlet failed to generate JSON file (in the hidden .vs folder).\n\nCheck your project's Saved\\Logs folder for the most recent .log file and open it in a text editor to look for 'LogBlueprintInspector:' messages to investigate why there is no JSON file.";
				icon = OLEMSGICON.OLEMSGICON_CRITICAL;
			}
			else
			{
				title = "Complete";
				message = "BlueprintInspector commandlet completed successfully.";

				if (bRestartVisualStudio)
				{
					message = "BlueprintInspector commandlet completed successfully.\n\nYou should RESTART Visual Studio!!!";
					icon = OLEMSGICON.OLEMSGICON_CRITICAL;
				}
			}

			VsShellUtilities.ShowMessageBox(BlueprintInspectorGlobals.package, message, title, icon, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}
	}
}
