using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Dispatching;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;

namespace HDRSnap2
{
    public sealed partial class MainWindow : Window
    {
        private DispatcherQueue _dispatcherQueue;
        private IntPtr _hwnd;
        private AppWindow _appWindow;
        private DxgiCapture _dxgiCapture;
        private Settings _settings;
        private AppHost _host;
        private SettingsWindow? _settingsWindow;
        private static readonly string SaveFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HDRSnap");

        public MainWindow()
        {
            this.InitializeComponent();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _dxgiCapture = new DxgiCapture();

            _appWindow.IsShownInSwitchers = false;
            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(-32000, -32000, 1, 1));

            Directory.CreateDirectory(SaveFolder);

            _settings = Settings.Load();
            _host = new AppHost(_settings);
            _host.CaptureRequested += () =>
                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () => await TakeScreenshot());
            _host.SettingsRequested += () =>
                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, OpenSettings);
            _host.Start();
        }

        private void OpenSettings()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }
            _settingsWindow = new SettingsWindow(_settings.Modifiers, _settings.Key);
            _settingsWindow.HotkeySaved += (mods, vk) => _host.UpdateHotkey(mods, vk);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Activate();
        }

        private async Task TakeScreenshot()
        {
            try
            {
                // Capture the HDR-correct screen up front so the selector preview
                // matches reality (not the washed GDI grab).
                var background = await _dxgiCapture.CaptureFullScreenAsync();
                int bgW = _dxgiCapture._lastWidth;
                int bgH = _dxgiCapture._lastHeight;

                var selector = new OverlaySelector();
                var region = await selector.GetSelectionAsync(background, bgW, bgH);

                if (region.Width < 5 || region.Height < 5)
                    return;

                // Crop the SAME frozen frame the user selected over -> true WYSIWYG.
                // Overlay coords are physical pixels, matching the captured frame 1:1,
                // so no DPI re-scale and no second capture are needed.
                int x = Math.Max(0, region.X);
                int y = Math.Max(0, region.Y);
                int w = Math.Min(region.Width, bgW - x);
                int h = Math.Min(region.Height, bgH - y);
                if (background == null || w <= 0 || h <= 0)
                    return;

                byte[] pixels = new byte[w * h * 4];
                for (int row = 0; row < h; row++)
                    Array.Copy(background, ((y + row) * bgW + x) * 4, pixels, row * w * 4, w * 4);

                Directory.CreateDirectory(SaveFolder);

                string filename = $"screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
                var storageFolder = await StorageFolder.GetFolderFromPathAsync(SaveFolder);
                var file = await storageFolder.CreateFileAsync(
                    filename, CreationCollisionOption.ReplaceExisting);

                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(
                        BitmapEncoder.PngEncoderId, stream);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        (uint)w, (uint)h, 96, 96, pixels);
                    await encoder.FlushAsync();
                }

                var dataPackage = new DataPackage();
                var streamRef = RandomAccessStreamReference.CreateFromFile(file);
                dataPackage.SetBitmap(streamRef);
                Clipboard.SetContent(dataPackage);

                System.Diagnostics.Debug.WriteLine($"Saved: {filename} ({w}x{h})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }
    }
}