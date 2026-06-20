using System;
using System.IO;

namespace HDRSnap2
{
    // Lightweight diagnostic logger. Writes to %LOCALAPPDATA%\HDRSnap2\diag.log so the
    // capture path can be inspected without a debugger attached.
    internal static class Log
    {
        private static readonly object _lock = new();
        public static readonly string Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HDRSnap2", "diag.log");

        public static void Line(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                    // Keep it bounded — reset if it grows past ~256 KB.
                    try { if (File.Exists(Path) && new FileInfo(Path).Length > 256 * 1024) File.Delete(Path); } catch { }
                    File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff}  {msg}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
