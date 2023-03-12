using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace BlueprintInspector
{
	public class NamedPipeThread
	{
		[DllImport("user32.dll")]
		static extern bool SetForegroundWindow(IntPtr hWnd);

		NamedPipeClientStream ClientPipe = null;
		NamedPipeServerStream ServerPipe = null;

		bool bIsPipeConnected = false;
		bool bShouldReadEditorWindowHandle = true;

		private List<string> BlueprintAssetPathToSend = new List<string>();

		public NamedPipeThread()
		{
		}

		public void Run()
		{
			while(true)
			{
				if (!bIsPipeConnected)  // do we need to open the named pipe?
				{
					try
					{
						// see if the named pipe already exists (opened by the other application)
						if (Directory.GetFiles("\\\\.\\pipe\\", "BlueprintInspector*").Length > 0)
						{
							ClientPipe = new NamedPipeClientStream(".", "BlueprintInspector");
							ClientPipe.Connect();
						}
						else  // otherwise, open the pipe as a server in bi-directional mode
						{
							ServerPipe = new NamedPipeServerStream("BlueprintInspector", PipeDirection.InOut);
							ServerPipe.WaitForConnection();
						}
					}
					catch(Exception ex)
					{
						Console.WriteLine("Exception: {0}", ex.Message);
					}

					bIsPipeConnected = true;
				}

				if (bShouldReadEditorWindowHandle && bIsPipeConnected)
				{
					bShouldReadEditorWindowHandle = false;

					if (ClientPipe != null)
					{
						byte[] handle = new byte[] { 0,0,0,0 };
						ClientPipe.Read(handle, 0, 4);
						BlueprintInspectorGlobals.EditorTopLevelWindowHandle = BitConverter.ToInt32(handle, 0);
					}
					else if (ServerPipe != null)
					{
						byte[] handle = new byte[] { 0,0,0,0 };
						ServerPipe.Read(handle, 0, 4);
						BlueprintInspectorGlobals.EditorTopLevelWindowHandle = BitConverter.ToInt32(handle, 0);
					}
				}

				if (BlueprintInspectorGlobals.bHasBlueprintAssetPathToSend)
				{
					lock (BlueprintInspectorGlobals.BlueprintAssetPathLock)
					{
						BlueprintAssetPathToSend.AddRange(BlueprintInspectorGlobals.BlueprintAssetPathToSend);
						BlueprintInspectorGlobals.BlueprintAssetPathToSend.Clear();

						BlueprintInspectorGlobals.bHasBlueprintAssetPathToSend = false;
					}

					try
					{
						while (BlueprintAssetPathToSend.Count > 0)
						{
							string command = String.Format("AssetPath,{0}", BlueprintAssetPathToSend[0]);

							byte[] outBuffer = Encoding.ASCII.GetBytes(command);
							ushort length = (ushort)outBuffer.Length;
							if (ClientPipe != null)
							{
								ClientPipe.WriteByte((byte)(length & 0xff));
								ClientPipe.WriteByte((byte)(length / 256));
								ClientPipe.Write(outBuffer, 0, length);
							}
							else if (ServerPipe != null)
							{
								ServerPipe.WriteByte((byte)(length & 0xff));
								ServerPipe.WriteByte((byte)(length / 256));
								ServerPipe.Write(outBuffer, 0, length);
							}

							BlueprintAssetPathToSend.RemoveAt(0);
						}

						IntPtr TopLevelWindow = new IntPtr(BlueprintInspectorGlobals.EditorTopLevelWindowHandle);
						SetForegroundWindow(TopLevelWindow);
					}
					catch(Exception)
					{
						// pipe connection was closed...
						bIsPipeConnected = false;
						bShouldReadEditorWindowHandle = true;

						if (ClientPipe != null)
						{
							ClientPipe.Close();
						}

						if (ServerPipe != null)
						{
							ServerPipe.Close();
						}

						ClientPipe = null;
						ServerPipe = null;
					}
				}

				Thread.Sleep(100);  // sleep for 1/10th of a second
			}
		}
	}
}
