using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace SharedProject
{
	internal class SharedGlobals
	{
#if OutputDebugString
		public static Mutex debug_mutex = null;  // for debugging output file
		private static bool BPCLFile_EnvChecked = false;
		private static string BPCLFile = "";
#endif

		public static int ReadCharSkipWhitespace(ref MemoryStream memorystream)
		{
			int somechar = memorystream.ReadByte();

			while(Char.IsWhiteSpace((char)somechar))  // skip whitespace
			{
				somechar = memorystream.ReadByte();
			}

			return somechar;
		}

		public static string ReadValue(ref MemoryStream memorystream)  // note: this does NOT read any trailing ',' character
		{
			int somechar = memorystream.ReadByte();

			if (somechar == -1)
			{
				return "";
			}

			while(Char.IsWhiteSpace((char)somechar))  // skip whitespace
			{
				somechar = memorystream.ReadByte();
			}

			if (somechar != '"')  // first character of value string should be "
			{
				return "";
			}

			string value = "";

			somechar = memorystream.ReadByte();

			while ((somechar != -1) && (somechar != '"'))  // read until second double quote adding char to token
			{
				value += (char)somechar;
				somechar = memorystream.ReadByte();
			}

			if (somechar == -1)
			{
				return "";
			}

			return value;
		}

		public static string ReadToken(ref MemoryStream memorystream)
		{
			int somechar = memorystream.ReadByte();

			if (somechar == -1)
			{
				return "";
			}

			while(Char.IsWhiteSpace((char)somechar))  // skip whitespace
			{
				somechar = memorystream.ReadByte();
			}

			if (somechar != '"')  // first character of token should be "
			{
				return "";
			}

			string token = "";

			somechar = memorystream.ReadByte();

			while ((somechar != -1) && (somechar != '"'))  // read until second double quote adding char to token
			{
				token += (char)somechar;
				somechar = memorystream.ReadByte();
			}

			if (somechar == -1)
			{
				return "";
			}

			somechar = memorystream.ReadByte();

			if (somechar != ':')
			{
				return "";
			}

			return token;
		}

#if OutputDebugString
		public static void OutputDebugString(string msg, bool bTimestamp = true)
		{
			if (debug_mutex == null)
			{
				return;
			}

			if (!BPCLFile_EnvChecked)
			{
				BPCLFile = Environment.GetEnvironmentVariable("BPCLFile");
				BPCLFile_EnvChecked = true;
			}

			if (BPCLFile == "")
			{
				return;
			}

			debug_mutex.WaitOne();

			try
			{
				using (StreamWriter sw = File.AppendText(BPCLFile))
				{
					sw.WriteLine(String.Format("{0}{1}", bTimestamp ? DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss - ") : "", msg));
				}
			}
			catch(Exception)
			{
			}

			debug_mutex.ReleaseMutex();
		}
#endif


		// From: https://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way
		[StructLayout(LayoutKind.Sequential)]
		public struct ParentProcessUtilities
		{
			// These members must match PROCESS_BASIC_INFORMATION
			internal IntPtr Reserved1;
			internal IntPtr PebBaseAddress;
			internal IntPtr Reserved2_0;
			internal IntPtr Reserved2_1;
			internal IntPtr UniqueProcessId;
			internal IntPtr InheritedFromUniqueProcessId;

			[DllImport("ntdll.dll")]
			private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtilities processInformation, int processInformationLength, out int returnLength);

			/// <summary>
			/// Gets the parent process of the current process.
			/// </summary>
			/// <returns>An instance of the Process class.</returns>
			public static Process GetParentProcess()
			{
				return GetParentProcess(Process.GetCurrentProcess().Handle);
			}

			/// <summary>
			/// Gets the parent process of specified process.
			/// </summary>
			/// <param name="id">The process id.</param>
			/// <returns>An instance of the Process class.</returns>
			public static Process GetParentProcess(int id)
			{
				Process process = Process.GetProcessById(id);
				return GetParentProcess(process.Handle);
			}

			/// <summary>
			/// Gets the parent process of a specified process.
			/// </summary>
			/// <param name="handle">The process handle.</param>
			/// <returns>An instance of the Process class.</returns>
			public static Process GetParentProcess(IntPtr handle)
			{
				ParentProcessUtilities pbi = new ParentProcessUtilities();
				int returnLength;
				int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
				if (status != 0)
				{
					return null;
				}

				try
				{
					return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
				}
				catch (ArgumentException)
				{
					// not found
					return null;
				}
			}
		}
	}

}
