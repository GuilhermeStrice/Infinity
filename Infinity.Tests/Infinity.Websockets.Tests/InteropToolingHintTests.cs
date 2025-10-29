using System.Diagnostics;

namespace Infinity.Websockets.Tests
{
	public class InteropToolingHintTests
	{
		private static bool HasCmd(string fileName, string args)
		{
			try
			{
				var p = Process.Start(new ProcessStartInfo
				{
					FileName = fileName,
					Arguments = args,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				});
				p.WaitForExit(2000);
				return p.ExitCode == 0;
			}
			catch { return false; }
		}

		[Fact]
		public void PrintInteropToolingMessageIfMissing()
		{
			bool hasBun = HasCmd("bun", "-v");
			bool hasNode = HasCmd("node", "-v") && HasCmd("node", "-e \"require('ws')\"");
			bool hasPy = (HasCmd("python3", "--version") || HasCmd("python", "--version")) && (HasCmd("python3", "-c \"import websockets\"") || HasCmd("python", "-c \"import websockets\""));

			if (!hasBun) Console.WriteLine("Interop hint: bun not found. Install bun to run Bun interop tests.");
			if (!hasNode) Console.WriteLine("Interop hint: node or ws not found. Install node and the 'ws' package to run Node interop tests.");
			if (!hasPy) Console.WriteLine("Interop hint: python or websockets not found. Install python and 'websockets' package to run Python interop tests.");
		}
	}
}


