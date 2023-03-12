using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace BlueprintInspector
{
	public class Common
	{
		public void WriteSharedMemoryData(MemoryMappedFile mmf, Mutex mutex)
		{
			if (mmf == null || mutex == null)
			{
				return;
			}

			mutex.WaitOne();

			try
			{
				using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, 4096))
				{
					accessor.Write(0, (ushort)BlueprintInspectorGlobals.JsonFileUpdateCounter);

					byte[] Buffer = ASCIIEncoding.ASCII.GetBytes(BlueprintInspectorGlobals.SolutionDirectory);
					accessor.Write(2, (ushort)Buffer.Length);
					accessor.WriteArray(4, Buffer, 0, Buffer.Length);
				}
			}
			catch(Exception)
			{
			}

			mutex.ReleaseMutex();
		}
	}
}
