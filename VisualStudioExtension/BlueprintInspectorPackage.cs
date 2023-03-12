using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using IServiceProvider = System.IServiceProvider;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;

#pragma warning disable VSSDK006

namespace BlueprintInspector
{
	/// <summary>
	/// This is the class that implements the package exposed by this assembly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The minimum requirement for a class to be considered a valid package for Visual Studio
	/// is to implement the IVsPackage interface and register itself with the shell.
	/// This package uses the helper classes defined inside the Managed Package Framework (MPF)
	/// to do it: it derives from the Package class that provides the implementation of the
	/// IVsPackage interface and uses the registration attributes defined in the framework to
	/// register itself and its components with the shell. These attributes tell the pkgdef creation
	/// utility what data to put into .pkgdef file.
	/// </para>
	/// <para>
	/// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
	/// </para>
	/// </remarks>
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[ProvideService(typeof(MyService), IsAsyncQueryable = true)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
	[Guid(GuidAndCmdID.PackageGuidString)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[ProvideBindingPath]
	public sealed class BlueprintInspectorPackage : AsyncPackage, IVsSolutionEvents, IVsSolutionLoadEvents, IOleCommandTarget
	{
		private IOleCommandTarget pkgCommandTarget;

		private IVsSolution2 advise_solution = null;
		private uint solutionEventsCookie = 0;

		private static Int32 VisualStudioProcessId = 0;

		private	static System.Threading.Thread NamedPipeWorkerThread = null;
		private static System.Threading.Thread RunExecutableThread = null;

		private static MemoryMappedFile mmf = null;
		private static Mutex mutex = null;

		private static char[] InvalidChars;

		private Guid OutputPaneGuid = Guid.Empty;

		#region Package Members

		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
		/// <param name="progress">A provider for progress updates.</param>
		/// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			await base.InitializeAsync(cancellationToken, progress);

			// When initialized asynchronously, the current thread may be a background thread at this point.
			// Do any initialization that requires the UI thread after switching to the UI thread.
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			BlueprintInspectorGlobals.package = this;

			this.pkgCommandTarget = await this.GetServiceAsync(typeof(IOleCommandTarget)) as IOleCommandTarget;

			// we want to find the Visual Studio process and use that process id to create a unique Mutex and MemoryMappedFile to communicate with the out-of-band CodeLens process (CodeLensProvider)
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
			SharedProject.SharedGlobals.debug_mutex = new Mutex(false, String.Format("BlueprintInspector_debugmutex{0}", VisualStudioProcessId), out bool debug_mutexCreated);

			if (SharedProject.SharedGlobals.debug_mutex != null)
			{
				SharedProject.SharedGlobals.OutputDebugString("", false);  // output a blank line each time Visual Studio is restarted
				SharedProject.SharedGlobals.OutputDebugString("InitializeAsync() - debug_mutex initialized");
				SharedProject.SharedGlobals.OutputDebugString(String.Format("InitializeAsync() - process id = {0}", VisualStudioProcessId));
			}
#endif

			mutex = new Mutex(false, String.Format("BlueprintInspector_mutex{0}", VisualStudioProcessId), out bool mutexCreated);

			try
			{
				try
				{
					mmf = MemoryMappedFile.CreateNew(String.Format("BlueprintInspector{0}", VisualStudioProcessId), 4096);
				}
				catch(Exception)
				{
					return;
				}

				if (mmf == null)
				{
					return;
				}

				UnadviseSolutionEvents();
				AdviseSolutionEvents();

				NamedPipeWorkerThread = new System.Threading.Thread(new NamedPipeThread().Run);
				NamedPipeWorkerThread.Priority = ThreadPriority.BelowNormal;
				NamedPipeWorkerThread.Start();  // start the thread running

				InvalidChars = Path.GetInvalidPathChars();  // get characters not allowed in file paths

				// see if we have a solution loaded...

				// we need the solution directory to determine where the JSON file is (it will be inside the ".vs" folder where the .sln file is)
				IVsSolution solution = (IVsSolution) Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(IVsSolution));
				if (solution != null)
				{
					solution.GetSolutionInfo(out string solutionDirectory, out string solutionName, out string solutionDirectory2);
					if (solutionDirectory != null && solutionDirectory != "")
					{
						BlueprintInspectorGlobals.SolutionDirectory = solutionDirectory;

						Common common = new Common();
						common.WriteSharedMemoryData(mmf, mutex);
					}
				}
			}
			catch(Exception)
			{
			}
		}

		private void AdviseSolutionEvents()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Get the solution interface
			advise_solution = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution2;
			if (advise_solution != null)
			{
				// Register for solution events
				advise_solution.AdviseSolutionEvents(this, out solutionEventsCookie);
			}
		}

		private void UnadviseSolutionEvents()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// Unadvise all events
			if (advise_solution != null && solutionEventsCookie != 0)
			{
				advise_solution.UnadviseSolutionEvents(solutionEventsCookie);
			}
		}

		#endregion

		#region IVsSolutionEvents

		// IVsSolutionEvents interface begin
		public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			try
			{
				// we need the solution directory to determine where the JSON file is (it will be inside the ".vs" folder where the .sln file is)
				IVsSolution solution = (IVsSolution) Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(IVsSolution));
				if (solution != null)
				{
					solution.GetSolutionInfo(out string solutionDirectory, out string solutionName, out string solutionDirectory2);
					if (solutionDirectory != null && solutionDirectory != "")
					{
						BlueprintInspectorGlobals.SolutionDirectory = solutionDirectory;

						Common common = new Common();
						common.WriteSharedMemoryData(mmf, mutex);
					}
				}
			}
			catch(Exception)
			{
			}

			return VSConstants.S_OK;
		}

		public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseSolution(object pUnkReserved)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterCloseSolution(object pUnkReserved)
		{
			return VSConstants.S_OK;
		}
		// IVsSolutionEvents interface end

		#endregion

		#region IVsSolutionLoadEvents

		// IVsSolutionLoadEvents interface begin
		public int OnBeforeOpenSolution(string pszSolutionFilename)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeBackgroundSolutionLoadBegins()
		{
			return VSConstants.S_OK;
		}

		public int OnQueryBackgroundLoadProjectBatch(out bool pfShouldDelayLoadToNextIdle)
		{
			pfShouldDelayLoadToNextIdle = false;

			return VSConstants.S_OK;
		}

		public int OnBeforeLoadProjectBatch(bool fIsBackgroundIdleBatch)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterLoadProjectBatch(bool fIsBackgroundIdleBatch)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterBackgroundSolutionLoadComplete()
		{
			return VSConstants.S_OK;
		}
		// IVsSolutionLoadEvents interface end

		#endregion

		#region IOleCommandTarget

		int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (pguidCmdGroup == GuidAndCmdID.guidCmdSet)
			{
				switch (prgCmds[0].cmdID)
				{
					case GuidAndCmdID.cmdidGenerateJsonFile:
						prgCmds[0].cmdf |= (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_INVISIBLE);
						return VSConstants.S_OK;
					case GuidAndCmdID.cmdidCopyToClipboard:
						prgCmds[0].cmdf |= (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_INVISIBLE);
						return VSConstants.S_OK;
					case GuidAndCmdID.cmdidOpenAssetPath:
						prgCmds[0].cmdf |= (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_INVISIBLE);
						return VSConstants.S_OK;
				}
			}

			return this.pkgCommandTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}

		int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (pguidCmdGroup == GuidAndCmdID.guidCmdSet)
			{
				switch (nCmdID)
				{
					case GuidAndCmdID.cmdidGenerateJsonFile:
					{
						try
						{
							string message = "";
							string title = "";

							if (RunExecutableThread != null && RunExecutableThread.IsAlive)
							{
								message = "BlueprintInspector commandlet is already running.\nWait until it finishes.";
								title = "WARNING!";

								VsShellUtilities.ShowMessageBox(this as IServiceProvider, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

								return VSConstants.S_OK;
							}

							// search the solution for a C++ project that contains a file with ".uproject" extension of the same name as the project...
							IVsSolution2 solution = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution2;

							if (solution == null)
							{
								message = "You must load an Unreal Engine solution file.";
								title = "WARNING!";

								VsShellUtilities.ShowMessageBox(this as IServiceProvider, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

								return VSConstants.S_OK;
							}

							if (solution != null)
							{
								solution.GetSolutionInfo(out string solutionDirectory, out string solutionName, out string solutionDirectory2);
								if (solutionDirectory == null || solutionDirectory == "")
								{
									message = "You must load an Unreal Engine solution file.";
									title = "WARNING!";

									VsShellUtilities.ShowMessageBox(this as IServiceProvider, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

									return VSConstants.S_OK;
								}

								BlueprintInspectorGlobals.SolutionDirectory = solutionDirectory;
							}

							IVsHierarchy solutionHierarchy = (IVsHierarchy)solution;
							IVsProject solutionProject = null;

							string UnrealEditorTargetFile = "";  // looking for "UnrealEditor.Target.cs" to find path where Engine is installed
							bool bIsUE4Project = false;

							GetHierarchyInSolution(solutionHierarchy, VSConstants.VSITEMID_ROOT, ref solutionProject, "Engine", "", "",
													out IVsHierarchy EngineHierarchy, out IVsProject EngineProject, out uint EngineItemId, out string Unused1);
							if ((EngineHierarchy != null) && (EngineProject != null))
							{
								GetHierarchyInSolution(EngineHierarchy, VSConstants.VSITEMID_ROOT, ref EngineProject, "UE5", "", "",
														out IVsHierarchy UE5Hierarchy, out IVsProject UE5Project, out uint UE5ItemId, out string Unused2);
								if ((UE5Hierarchy != null) && (UE5Project != null))
								{
									GetHierarchyInSolution(UE5Hierarchy, VSConstants.VSITEMID_ROOT, ref UE5Project, "", "Source", "",
															out IVsHierarchy SourceHierarchy, out IVsProject SourceProject, out uint SourceItemId, out string Unused3);
									if ((SourceHierarchy != null) && (SourceProject != null))
									{
										GetHierarchyInSolution(SourceHierarchy, SourceItemId, ref SourceProject, "", "", "UnrealEditor.Target.cs",
																out IVsHierarchy UnusedHierarchy, out IVsProject UnusedProject, out uint UnusedItemId, out UnrealEditorTargetFile);
									}
								}
								else  // if UE5 not found, then check if UE4...
								{
									GetHierarchyInSolution(EngineHierarchy, VSConstants.VSITEMID_ROOT, ref EngineProject, "UE4", "", "",
															out IVsHierarchy UE4Hierarchy, out IVsProject UE4Project, out uint UE4ItemId, out string Unused4);
									if ((UE4Hierarchy != null) && (UE4Project != null))
									{
										GetHierarchyInSolution(UE4Hierarchy, VSConstants.VSITEMID_ROOT, ref UE4Project, "", "Source", "",
																out IVsHierarchy SourceHierarchy, out IVsProject SourceProject, out uint SourceItemId, out string Unused3);
										if ((SourceHierarchy != null) && (SourceProject != null))
										{
											GetHierarchyInSolution(SourceHierarchy, SourceItemId, ref SourceProject, "", "", "UE4Editor.Target.cs",
																	out IVsHierarchy UnusedHierarchy, out IVsProject UnusedProject, out uint UnusedItemId, out UnrealEditorTargetFile);

											bIsUE4Project = true;
										}
									}
								}
							}

							if (UnrealEditorTargetFile == "")
							{
								message = "Can't determine which Unreal Engine version is being used.";
								title = "WARNING!";

								VsShellUtilities.ShowMessageBox(this as IServiceProvider, message, title, OLEMSGICON.OLEMSGICON_WARNING, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

								return VSConstants.S_OK;
							}

							string UnrealEditorTargetDirectory = Path.GetDirectoryName(UnrealEditorTargetFile);
							DirectoryInfo EngineDirectoryInfo = System.IO.Directory.GetParent(UnrealEditorTargetDirectory);
							BlueprintInspectorGlobals.EngineDirectory = EngineDirectoryInfo.FullName;  // without the trailing "\\"

							List<string> projectFilenames = new List<string>();

				            IEnumHierarchies enumerator = null;
				            Guid guid = Guid.Empty;
				            solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref guid, out enumerator);

				            IVsHierarchy[] hierarchy = new IVsHierarchy[1] { null };
				            uint fetched = 0;
				            for (enumerator.Reset(); enumerator.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1; /*nothing*/)
				            {
				                IVsProject Project = (IVsProject)hierarchy[0];

								GetProjectFilesInProject(hierarchy[0], VSConstants.VSITEMID_ROOT, ref Project, ref projectFilenames);
				            }

							if (projectFilenames.Count == 0)
							{
								message = "Can't find .uproject file in solution.  You must run the commandlet manually.";
								title = "ERROR!";

								VsShellUtilities.ShowMessageBox(this as IServiceProvider, message, title, OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

								return VSConstants.S_OK;
							}

							string projectName = "";

							SelectProjectForm form = new SelectProjectForm();
							form.ProjectFiles = projectFilenames;

							System.Windows.Forms.DialogResult result = form.ShowDialog();

							if (result != DialogResult.OK)
							{
								return VSConstants.S_OK;
							}

							bool bIncludeEngine = form.bIncludeEngine;
							bool bIncludePlugins = form.bIncludePlugins;
							bool bIncludeDevelopers = form.bIncludeDevelopers;

							BlueprintInspectorGlobals.ProjectFilename = projectFilenames[form.SelectedIndex];

							projectName = Path.GetFileNameWithoutExtension(projectFilenames[form.SelectedIndex]);

							// we now have the Engine directory (for Engine\Binaries\Win64\UnrealEditor.exe) and the project file name (*.uproject) so we can run the commandlet to generate the JSON file...
							if (OutputPaneGuid == Guid.Empty)
							{
								// Create a new output pane.
								IVsOutputWindow output = GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;

								OutputPaneGuid = Guid.NewGuid();

								bool visible = true;
								bool clearWithSolution = false;
								output.CreatePane(ref OutputPaneGuid, "Blueprint Inspector", Convert.ToInt32(visible), Convert.ToInt32(clearWithSolution));
								output.GetPane(ref OutputPaneGuid, out BlueprintInspectorGlobals.OutputPane);
							}
							else
							{
								// clear the output pane
								if (BlueprintInspectorGlobals.OutputPane != null)
								{
									BlueprintInspectorGlobals.OutputPane.Clear();
								}
							}

							if (BlueprintInspectorGlobals.OutputPane != null)
							{
								BlueprintInspectorGlobals.OutputPane.Activate();
								BlueprintInspectorGlobals.OutputPane.OutputStringThreadSafe("Running BlueprintInspector commandlet now...");
								BlueprintInspectorGlobals.OutputPane.OutputStringThreadSafe("\n");
							}

							RunExecutableThread = new System.Threading.Thread(new RunExecutable(bIsUE4Project, bIncludeEngine, bIncludePlugins, bIncludeDevelopers).Run);
							RunExecutableThread.Priority = ThreadPriority.Normal;
							RunExecutableThread.Start();  // start the thread running
						}
						catch(Exception)
						{
						}
					}
					return VSConstants.S_OK;

					case GuidAndCmdID.cmdidCopyToClipboard:
					{
						try
						{
							if (IsQueryParameterList(pvaIn, pvaOut, nCmdexecopt))
							{
								return VSConstants.S_OK;
							}
							else
							{
								if (pvaIn == IntPtr.Zero)
								{
									return VSConstants.S_FALSE;
								}

								object vaInObject = Marshal.GetObjectForNativeVariant(pvaIn);
								if (vaInObject == null || vaInObject.GetType() != typeof(string))
								{
									return VSConstants.E_INVALIDARG;
								}

								string AssetPathsString = (string)vaInObject;
								string[] AssetPaths = AssetPathsString.Split(',');

								string OutputString = "";
								foreach (string AssetPath in AssetPaths)
								{
									OutputString += AssetPath + "\n";
								}

								Clipboard.SetText(OutputString);
							}
						}
						catch(Exception)
						{
						}
					}
					return VSConstants.S_OK;

					case GuidAndCmdID.cmdidOpenAssetPath:
					{
						try
						{
							if (IsQueryParameterList(pvaIn, pvaOut, nCmdexecopt))
							{
								return VSConstants.S_OK;
							}
							else
							{
								if (pvaIn == IntPtr.Zero)
								{
									return VSConstants.S_FALSE;
								}

								object vaInObject = Marshal.GetObjectForNativeVariant(pvaIn);
								if (vaInObject == null || vaInObject.GetType() != typeof(string))
								{
									return VSConstants.E_INVALIDARG;
								}

								string AssetPathsString = (string)vaInObject;

								lock (BlueprintInspectorGlobals.BlueprintAssetPathLock)
								{
									BlueprintInspectorGlobals.BlueprintAssetPathToSend.Add(AssetPathsString);
									BlueprintInspectorGlobals.bHasBlueprintAssetPathToSend = true;
								}
							}
						}
						catch(Exception)
						{
						}
					}
					return VSConstants.S_OK;
				}
			}

			return this.pkgCommandTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		#endregion

		private static bool IsQueryParameterList(IntPtr pvaIn, IntPtr pvaOut, uint nCmdexecopt)
		{
			ushort lo = (ushort)(nCmdexecopt & (uint)0xffff);
			ushort hi = (ushort)(nCmdexecopt >> 16);
			if (lo == (ushort)OLECMDEXECOPT.OLECMDEXECOPT_SHOWHELP)
			{
				if (hi == VsMenus.VSCmdOptQueryParameterList)
				{
					return true;
				}
			}

			return false;
		}

		private void GetProjectFilesInProject(IVsHierarchy hierarchy, uint itemId, ref IVsProject Project, ref List<string> projectFilenames)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			try
			{
				// NOTE: If itemId == VSConstants.VSITEMID_ROOT then this hierarchy is a solution, project, or folder in the Solution Explorer

				if (hierarchy == null)
				{
					return;
				}

				object ChildObject = null;

				// Get the first visible child node
				if (hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out ChildObject) == VSConstants.S_OK)
				{
					while (ChildObject != null)
					{
						if ((ChildObject is int) && ((uint)(int)ChildObject == VSConstants.VSITEMID_NIL))
						{
							break;
						}

						uint visibleChildNodeId = Convert.ToUInt32(ChildObject);

						string projectFilename = "";

						try
						{
							if (Project.GetMkDocument(visibleChildNodeId, out projectFilename) == VSConstants.S_OK)
							{
								if ((projectFilename != null) && (projectFilename.Length > 0) &&
									(!projectFilename.EndsWith("\\")) &&  // some invalid "filenames" will end with '\\'
									(projectFilename.IndexOfAny(InvalidChars) == -1) &&
									(projectFilename.IndexOf(":", StringComparison.OrdinalIgnoreCase) == 1))  // make sure filename is of the form: drive letter followed by colon
								{

									if (projectFilename.EndsWith(".uproject"))
									{
										projectFilenames.Add(projectFilename);
									}
								}
							}
						}
						catch (Exception)
						{
						}

						ChildObject = null;

						// Get the next visible sibling node
						if (hierarchy.GetProperty(visibleChildNodeId, (int)__VSHPROPID.VSHPROPID_NextVisibleSibling, out ChildObject) != VSConstants.S_OK)
						{
							break;
						}
					}
				}
			}
			catch(Exception)
			{
			}
		}

		// searches the solution for a specific project name, folder name, or file name
		private void GetHierarchyInSolution(IVsHierarchy hierarchy, uint itemId, ref IVsProject Project, string SearchProject, string SearchFolder, string SearchFileName, out IVsHierarchy OutHierarchy, out IVsProject OutProject, out uint OutItemId, out string OutFilename)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			OutHierarchy = null;
			OutProject = null;
			OutItemId = VSConstants.VSITEMID_ROOT;
			OutFilename = "";

			try
			{
				// NOTE: If itemId == VSConstants.VSITEMID_ROOT then this hierarchy is a solution, project, or folder in the Solution Explorer

				if (hierarchy == null)
				{
					return;
				}

				object ChildObject = null;

				// Get the first visible child node
				if (hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out ChildObject) == VSConstants.S_OK)
				{
					while (ChildObject != null)
					{
						if ((ChildObject is int) && ((uint)(int)ChildObject == VSConstants.VSITEMID_NIL))
						{
							break;
						}

						uint visibleChildNodeId = Convert.ToUInt32(ChildObject);

						object nameObject = null;

						if ((hierarchy.GetProperty(visibleChildNodeId, (int)__VSHPROPID.VSHPROPID_Name, out nameObject) == VSConstants.S_OK) && (nameObject != null))
						{
							if ((string)nameObject == SearchProject)
							{
								Guid nestedHierarchyGuid = typeof(IVsHierarchy).GUID;
								IntPtr nestedHiearchyValue = IntPtr.Zero;
								uint nestedItemIdValue = 0;

								// see if the child node has a nested hierarchy (i.e. is it a project?, is it a folder?, etc.)...
								if ((hierarchy.GetNestedHierarchy(visibleChildNodeId, ref nestedHierarchyGuid, out nestedHiearchyValue, out nestedItemIdValue) == VSConstants.S_OK) &&
									(nestedHiearchyValue != IntPtr.Zero && nestedItemIdValue == VSConstants.VSITEMID_ROOT))
								{
									// Get the new hierarchy
									IVsHierarchy nestedHierarchy = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(nestedHiearchyValue) as IVsHierarchy;
									System.Runtime.InteropServices.Marshal.Release(nestedHiearchyValue);

									if (nestedHierarchy != null)
									{
										OutHierarchy = nestedHierarchy;
										OutProject = (IVsProject)nestedHierarchy;

										return;
									}
								}
							}
							else
							{
								object NodeChildObject = null;

								// see if this regular node has children...
								if ((string)nameObject == SearchFolder)
								{
									if (hierarchy.GetProperty(visibleChildNodeId, (int)__VSHPROPID.VSHPROPID_FirstVisibleChild, out NodeChildObject) == VSConstants.S_OK)
									{
										if (NodeChildObject != null)
										{
											if ((NodeChildObject is int) && ((uint)(int)NodeChildObject != VSConstants.VSITEMID_NIL))
											{
												OutHierarchy = hierarchy;
												OutProject = Project;
												OutItemId = visibleChildNodeId;
											}
										}
									}
								}

								if ((string)nameObject == SearchFileName)
								{
									try
									{
										string projectFilename = "";

										if (Project.GetMkDocument(visibleChildNodeId, out projectFilename) == VSConstants.S_OK)
										{
											if ((projectFilename != null) && (projectFilename.Length > 0) &&
												(!projectFilename.EndsWith("\\")) &&  // some invalid "filenames" will end with '\\'
												(projectFilename.IndexOfAny(InvalidChars) == -1) &&
												(projectFilename.IndexOf(":", StringComparison.OrdinalIgnoreCase) == 1))  // make sure filename is of the form: drive letter followed by colon
											{
												OutFilename = projectFilename;
											}
										}
									}
									catch (Exception)
									{
									}
								}
							}
						}

						ChildObject = null;

						// Get the next visible sibling node
						if (hierarchy.GetProperty(visibleChildNodeId, (int)__VSHPROPID.VSHPROPID_NextVisibleSibling, out ChildObject) != VSConstants.S_OK)
						{
							break;
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}
	}
}
