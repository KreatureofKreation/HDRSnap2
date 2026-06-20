using System;
using System.IO;

namespace HDRSnap2
{
    // Manages the "start at login" shortcut in the user's Startup folder.
    // Uses WScript.Shell via COM so no extra package reference is needed.
    public static class Startup
    {
        private static string LnkPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup), "HDRSnap2.lnk");

        public static bool IsEnabled() => File.Exists(LnkPath);

        public static void Toggle()
        {
            if (IsEnabled())
            {
                try { File.Delete(LnkPath); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Startup remove: " + ex.Message); }
            }
            else
            {
                CreateShortcut();
            }
        }

        private static void CreateShortcut()
        {
            try
            {
                string exe = Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(exe)) return;

                Type? t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;

                dynamic shell = Activator.CreateInstance(t)!;
                dynamic sc = shell.CreateShortcut(LnkPath);
                sc.TargetPath = exe;
                sc.WorkingDirectory = Path.GetDirectoryName(exe);
                sc.Save();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Startup add: " + ex.Message); }
        }
    }
}
