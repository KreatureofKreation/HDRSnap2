using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HDRSnap2
{
    // Owns a single hidden message window on a dedicated background thread that
    // handles the global hotkey, the system-tray icon, and its context menu.
    // Replaces the old HotkeyHelper.
    public class AppHost
    {
        public event Action? CaptureRequested;
        public event Action? SettingsRequested;

        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? n);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern ushort RegisterClassEx(ref WNDCLASSEX c);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr lp);
        [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
        [DllImport("user32.dll")] static extern void PostQuitMessage(int code);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string? item);
        [DllImport("user32.dll")] static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern IntPtr LoadIcon(IntPtr inst, IntPtr name);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBox(IntPtr h, string text, string caption, uint type);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA data);

        [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEX { public uint cbSize, style; public WndProcDelegate lpfnWndProc; public int cbClsExtra, cbWndExtra; public IntPtr hInstance, hIcon, hCursor, hbrBackground; public string? lpszMenuName; public string lpszClassName; public IntPtr hIconSm; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID, uFlags, uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState, dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        const uint WM_APP = 0x8000;
        const uint WM_TRAYICON = WM_APP + 1;
        const uint WM_REHOTKEY = WM_APP + 2;
        const int WM_HOTKEY = 0x0312;
        const uint WM_DESTROY = 0x0002;
        const uint WM_NULL = 0x0000;
        const uint WM_LBUTTONUP = 0x0202;
        const uint WM_RBUTTONUP = 0x0205;
        const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
        const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
        const uint MF_STRING = 0, MF_CHECKED = 8, MF_SEPARATOR = 0x800;
        const uint TPM_RETURNCMD = 0x0100, TPM_RIGHTBUTTON = 2;
        const uint MOD_NOREPEAT = 0x4000;
        const int HOTKEY_ID = 1;
        const uint ID_CAPTURE = 1, ID_OPENFOLDER = 2, ID_STARTUP = 3, ID_SETTINGS = 4, ID_ABOUT = 5, ID_EXIT = 6;

        readonly Settings _settings;
        IntPtr _hwnd;
        IntPtr _icon;
        WndProcDelegate? _wndProc;
        NOTIFYICONDATA _nid;
        uint _pendingMods, _pendingVk;
        Thread? _thread;

        public AppHost(Settings settings) => _settings = settings;

        public void Start()
        {
            _thread = new Thread(Run) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        // Called from the UI thread; re-registration runs on the host thread.
        public void UpdateHotkey(uint mods, uint vk)
        {
            _pendingMods = mods; _pendingVk = vk;
            if (_hwnd != IntPtr.Zero) PostMessage(_hwnd, WM_REHOTKEY, IntPtr.Zero, IntPtr.Zero);
        }

        void Run()
        {
            var hInst = GetModuleHandle(null);
            _wndProc = WndProc;
            const string cls = "HDRSnapHost";
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _wndProc,
                lpszClassName = cls,
                hInstance = hInst
            };
            RegisterClassEx(ref wc);
            _hwnd = CreateWindowEx(0, cls, "HDRSnap2", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);

            RegisterHotKey(_hwnd, HOTKEY_ID, _settings.Modifiers | MOD_NOREPEAT, _settings.Key);

            _icon = LoadTrayIcon();
            _nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _icon,
                szTip = "HDRSnap2 — " + _settings.HotkeyText,
                szInfo = "",
                szInfoTitle = ""
            };
            bool added = Shell_NotifyIcon(NIM_ADD, ref _nid);
            System.Diagnostics.Debug.WriteLine($"Tray icon added: {added}");

            while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            if (_icon != IntPtr.Zero) DestroyIcon(_icon);
        }

        IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                switch (msg)
                {
                    case WM_HOTKEY:
                        CaptureRequested?.Invoke();
                        return IntPtr.Zero;

                    case WM_TRAYICON:
                        uint ev = (uint)(lParam.ToInt64() & 0xFFFF);
                        if (ev == WM_LBUTTONUP) CaptureRequested?.Invoke();
                        else if (ev == WM_RBUTTONUP) ShowMenu();
                        return IntPtr.Zero;

                    case WM_REHOTKEY:
                        ReRegisterHotkey();
                        return IntPtr.Zero;

                    case WM_DESTROY:
                        PostQuitMessage(0);
                        return IntPtr.Zero;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Host WndProc: " + ex); }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        void ShowMenu()
        {
            var menu = CreatePopupMenu();
            AppendMenu(menu, MF_STRING, ID_CAPTURE, "Capture now");
            AppendMenu(menu, MF_STRING, ID_OPENFOLDER, "Open screenshots folder");
            AppendMenu(menu, MF_SEPARATOR, 0, null);
            AppendMenu(menu, MF_STRING | (Startup.IsEnabled() ? MF_CHECKED : 0), ID_STARTUP, "Start with Windows");
            AppendMenu(menu, MF_STRING, ID_SETTINGS, "Settings…");
            AppendMenu(menu, MF_STRING, ID_ABOUT, "About");
            AppendMenu(menu, MF_SEPARATOR, 0, null);
            AppendMenu(menu, MF_STRING, ID_EXIT, "Exit");

            GetCursorPos(out POINT pt);
            SetForegroundWindow(_hwnd); // so the menu dismisses when clicking elsewhere
            int cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.x, pt.y, 0, _hwnd, IntPtr.Zero);
            PostMessage(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            DestroyMenu(menu);

            switch ((uint)cmd)
            {
                case ID_CAPTURE: CaptureRequested?.Invoke(); break;
                case ID_OPENFOLDER: OpenFolder(); break;
                case ID_STARTUP: Startup.Toggle(); break;
                case ID_SETTINGS: SettingsRequested?.Invoke(); break;
                case ID_ABOUT:
                    MessageBox(_hwnd, "HDRSnap2\nHDR-accurate screenshots.\n\nHotkey: " + _settings.HotkeyText, "HDRSnap2", 0);
                    break;
                case ID_EXIT: ExitApp(); break;
            }
        }

        void ReRegisterHotkey()
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            if (RegisterHotKey(_hwnd, HOTKEY_ID, _pendingMods | MOD_NOREPEAT, _pendingVk))
            {
                _settings.Modifiers = _pendingMods;
                _settings.Key = _pendingVk;
                _settings.Save();
                _nid.szTip = "HDRSnap2 — " + _settings.HotkeyText;
                Shell_NotifyIcon(NIM_MODIFY, ref _nid);
            }
            else
            {
                RegisterHotKey(_hwnd, HOTKEY_ID, _settings.Modifiers | MOD_NOREPEAT, _settings.Key); // revert
                MessageBox(_hwnd,
                    "Could not set hotkey " + Settings.HotkeyToText(_pendingMods, _pendingVk) +
                    " (already in use by another app).\n\nKept previous: " + _settings.HotkeyText,
                    "HDRSnap2", 0);
            }
        }

        void OpenFolder()
        {
            try
            {
                var folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HDRSnap");
                System.IO.Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("OpenFolder: " + ex.Message); }
        }

        void ExitApp()
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            if (_icon != IntPtr.Zero) DestroyIcon(_icon);
            Environment.Exit(0);
        }

        IntPtr LoadTrayIcon()
        {
            try
            {
                using var bmp = new System.Drawing.Bitmap(32, 32);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(System.Drawing.Color.Transparent);
                    using var bg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(232, 106, 51));
                    g.FillRectangle(bg, 2, 2, 28, 28);
                    using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 3f);
                    g.DrawRectangle(pen, 9, 9, 14, 14); // crop-rectangle motif
                }
                return bmp.GetHicon();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Tray icon: " + ex.Message); }
            return LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION fallback
        }
    }
}
