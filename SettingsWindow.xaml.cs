using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace HDRSnap2
{
    public sealed partial class SettingsWindow : Window
    {
        // Fires with the chosen (modifiers, virtualKey) when the user saves.
        public event Action<uint, uint>? HotkeySaved;

        private uint _mods;
        private uint _vk;
        private bool _recording;

        public SettingsWindow(uint mods, uint vk)
        {
            this.InitializeComponent();
            _mods = mods;
            _vk = vk;

            Title = "HDRSnap2 Settings";
            RecordButton.Content = Settings.HotkeyToText(mods, vk);

            // AppWindow.Resize is in physical pixels — scale by DPI so the window is
            // the right size on high-DPI displays (e.g. 300% scaling).
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            double scale = GetDpiForWindow(hwnd) / 96.0;
            if (scale <= 0) scale = 1;
            AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(440 * scale), (int)(300 * scale)));
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
            {
                p.IsResizable = false;
                p.IsMaximizable = false;
            }

            ((FrameworkElement)Content).KeyDown += OnKeyDown;
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            _recording = true;
            RecordButton.Content = "Press your combo…";
            HintText.Text = "Listening… press Ctrl/Alt/Shift/Win + a key.";
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_recording) return;

            var key = e.Key;
            // Ignore bare modifier presses — wait for the actual key.
            if (key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu
                or VirtualKey.LeftWindows or VirtualKey.RightWindows)
            {
                e.Handled = true;
                return;
            }

            uint mods = 0;
            if (IsDown(VirtualKey.Control)) mods |= Settings.MOD_CONTROL;
            if (IsDown(VirtualKey.Menu)) mods |= Settings.MOD_ALT;
            if (IsDown(VirtualKey.Shift)) mods |= Settings.MOD_SHIFT;
            if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) mods |= Settings.MOD_WIN;

            e.Handled = true;

            if (mods == 0)
            {
                HintText.Text = "Need at least one modifier (Ctrl / Alt / Shift / Win). Try again.";
                return;
            }

            _mods = mods;
            _vk = (uint)key;
            _recording = false;
            RecordButton.Content = Settings.HotkeyToText(_mods, _vk);
            HintText.Text = "Press Save to apply.";
        }

        private static bool IsDown(VirtualKey k) =>
            InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(CoreVirtualKeyStates.Down);

        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _recording = false;
            HotkeySaved?.Invoke(_mods, _vk);
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
