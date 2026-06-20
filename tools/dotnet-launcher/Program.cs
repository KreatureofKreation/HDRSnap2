using System;
using System.Diagnostics;
using System.IO;

// Tiny launcher that sits at the top of the bundle and starts the real app
// from the .\app subfolder (so the runtime DLLs stay tucked away there).
class Program
{
    static void Main()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;            // launcher's own folder (not temp)
            string appDir = Path.Combine(baseDir, "app");
            string appExe = Path.Combine(appDir, "HDRSnap2.exe");

            Process.Start(new ProcessStartInfo
            {
                FileName = appExe,
                WorkingDirectory = appDir,    // so the app finds its DLLs in .\app
                UseShellExecute = true
            });
        }
        catch
        {
            // nothing useful to do if it fails to launch
        }
    }
}
