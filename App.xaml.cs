using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HDRSnap2
{
    public partial class App : Application
    {
        private MainWindow? _window;
        private static Mutex? _mutex;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Single instance: a second launch exits so we never get duplicate
            // tray icons or fight over the global hotkey.
            _mutex = new Mutex(true, @"Local\HDRSnap2_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                // Already running — tell the user, then terminate this duplicate hard.
                MessageBox(IntPtr.Zero,
                    "HDRSnap2 is already running.\n\nLook for its icon in the system tray (click the ^ arrow near the clock).",
                    "HDRSnap2", 0x40 /* MB_ICONINFORMATION */);
                Environment.Exit(0);
            }

            try
            {
                _window = new MainWindow();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Launch error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }
    }
}