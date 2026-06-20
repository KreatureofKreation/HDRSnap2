using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace HDRSnap2
{
    public class OverlaySelector
    {
        private TaskCompletionSource<System.Drawing.Rectangle> _tcs = new();

        public async Task<System.Drawing.Rectangle> GetSelectionAsync(byte[]? background = null, int bgWidth = 0, int bgHeight = 0)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    var form = new OverlayForm { Background = background, BgWidth = bgWidth, BgHeight = bgHeight };
                    form.SelectionComplete += rect => _tcs.TrySetResult(rect);
                    form.Run();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Overlay error: " + ex.Message);
                    _tcs.TrySetResult(System.Drawing.Rectangle.Empty);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return await _tcs.Task;
        }
    }

    public class OverlayForm
    {
        public event Action<System.Drawing.Rectangle>? SelectionComplete;
        public byte[]? Background;
        public int BgWidth;
        public int BgHeight;

        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? lpModuleName);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool UpdateWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
        [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG lpMsg);
        [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
        [DllImport("user32.dll")] static extern bool SetCapture(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] static extern void PostQuitMessage(int nExitCode);
        [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);
        [DllImport("gdi32.dll")] static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);
        [DllImport("gdi32.dll")] static extern IntPtr GetStockObject(int fnObject);
        [DllImport("gdi32.dll")] static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);
        [DllImport("msimg32.dll")] static extern bool AlphaBlend(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, BLENDFUNCTION bf);

        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX; public int ptY; }
        [StructLayout(LayoutKind.Sequential)]
        struct PAINTSTRUCT { public IntPtr hdc; public bool fErase; public int left, top, right, bottom; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEX
        {
            public uint cbSize; public uint style; public WndProcDelegate lpfnWndProc; public int cbClsExtra;
            public int cbWndExtra; public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor;
            public IntPtr hbrBackground; public string? lpszMenuName; public string lpszClassName; public IntPtr hIconSm;
        }

        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        const uint WS_POPUP = 0x80000000;
        const uint WS_EX_TOPMOST = 0x00000008;
        const uint WS_EX_TOOLWINDOW = 0x00000080;
        const uint WM_PAINT = 0x000F;
        const uint WM_LBUTTONDOWN = 0x0201;
        const uint WM_LBUTTONUP = 0x0202;
        const uint WM_MOUSEMOVE = 0x0200;
        const uint WM_KEYDOWN = 0x0100;
        const uint WM_DESTROY = 0x0002;
        const uint WM_ERASEBKGND = 0x0014;
        const int VK_ESCAPE = 0x1B;
        const int IDC_CROSS = 32515;
        const int NULL_BRUSH = 5;
        const uint SRCCOPY = 0x00CC0020;

        private IntPtr _hwnd;
        private IntPtr _hBitmap;
        private IntPtr _memDC;
        private IntPtr _dimBmp;
        private IntPtr _dimDC;
        private IntPtr _backBmp;
        private IntPtr _backDC;
        private bool _isDrawing;
        private int _startX, _startY, _endX, _endY;
        private int _screenW, _screenH;
        private WndProcDelegate? _wndProc;
        private string _className = "HDRSnapOverlay2";
        private IntPtr _hInstance;
        private static int _classCounter;

        public void Run()
        {
            _screenW = GetSystemMetrics(0);
            _screenH = GetSystemMetrics(1);

            System.Diagnostics.Debug.WriteLine($"Screen: {_screenW}x{_screenH}");

            CaptureScreen();

            _hInstance = GetModuleHandle(null);
            _className = "HDRSnapOverlay_" + System.Threading.Interlocked.Increment(ref _classCounter);
            _wndProc = WndProc;

            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                lpfnWndProc = _wndProc,
                lpszClassName = _className,
                hInstance = _hInstance,
                hCursor = LoadCursor(IntPtr.Zero, IDC_CROSS),
                hbrBackground = IntPtr.Zero,
                style = 0
            };

            var atom = RegisterClassEx(ref wc);
            System.Diagnostics.Debug.WriteLine($"RegisterClassEx result: {atom}, error: {Marshal.GetLastWin32Error()}");

            _hwnd = CreateWindowEx(
                WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
                _className, "",
                WS_POPUP,
                0, 0, _screenW, _screenH,
                IntPtr.Zero, IntPtr.Zero, _hInstance, IntPtr.Zero);

            System.Diagnostics.Debug.WriteLine($"CreateWindowEx hwnd: {_hwnd}, error: {Marshal.GetLastWin32Error()}");

            if (_hwnd == IntPtr.Zero)
            {
                UnregisterClass(_className, _hInstance);
                SelectionComplete?.Invoke(System.Drawing.Rectangle.Empty);
                return;
            }

            ShowWindow(_hwnd, 5);
            SetForegroundWindow(_hwnd);
            UpdateWindow(_hwnd);

            while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            DestroyResources();
            UnregisterClass(_className, _hInstance);
        }

        private void CaptureScreen()
        {
            Bitmap bmp;
            if (Background != null && BgWidth > 0 && BgHeight > 0)
            {
                // HDR-correct background captured via DXGI — matches the saved image.
                bmp = new Bitmap(BgWidth, BgHeight, PixelFormat.Format32bppArgb);
                var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, BgWidth, BgHeight),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                Marshal.Copy(Background, 0, data.Scan0, Background.Length);
                bmp.UnlockBits(data);
            }
            else
            {
                // Fallback: GDI grab (looks washed on HDR, but only the preview).
                bmp = new Bitmap(_screenW, _screenH, PixelFormat.Format32bppArgb);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.CopyFromScreen(0, 0, 0, 0, new Size(_screenW, _screenH));
            }
            _hBitmap = bmp.GetHbitmap();
            bmp.Dispose();

            IntPtr screenDC = GetDC(IntPtr.Zero);

            // Original screenshot.
            _memDC = CreateCompatibleDC(screenDC);
            SelectObject(_memDC, _hBitmap);

            // Pre-dimmed screenshot, built once so WM_PAINT does no per-frame allocation.
            _dimBmp = CreateCompatibleBitmap(screenDC, _screenW, _screenH);
            _dimDC = CreateCompatibleDC(screenDC);
            SelectObject(_dimDC, _dimBmp);
            BitBlt(_dimDC, 0, 0, _screenW, _screenH, _memDC, 0, 0, SRCCOPY);
            DimOnce(screenDC);

            // Back buffer for flicker-free compositing.
            _backBmp = CreateCompatibleBitmap(screenDC, _screenW, _screenH);
            _backDC = CreateCompatibleDC(screenDC);
            SelectObject(_backDC, _backBmp);

            ReleaseDC(IntPtr.Zero, screenDC);
        }

        // Blend a constant-alpha black layer over the dim buffer (once).
        private void DimOnce(IntPtr screenDC)
        {
            using var black = new Bitmap(_screenW, _screenH);
            using (var g = System.Drawing.Graphics.FromImage(black))
                g.Clear(Color.Black);
            IntPtr blackHbmp = black.GetHbitmap();
            IntPtr blackDC = CreateCompatibleDC(screenDC);
            IntPtr oldBlack = SelectObject(blackDC, blackHbmp);
            AlphaBlend(_dimDC, 0, 0, _screenW, _screenH, blackDC, 0, 0, _screenW, _screenH,
                new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 120, AlphaFormat = 0 });
            SelectObject(blackDC, oldBlack);
            DeleteObject(blackHbmp);
            DeleteDC(blackDC);
        }

        private void DestroyResources()
        {
            if (_backDC != IntPtr.Zero) DeleteDC(_backDC);
            if (_backBmp != IntPtr.Zero) DeleteObject(_backBmp);
            if (_dimDC != IntPtr.Zero) DeleteDC(_dimDC);
            if (_dimBmp != IntPtr.Zero) DeleteObject(_dimBmp);
            if (_memDC != IntPtr.Zero) DeleteDC(_memDC);
            if (_hBitmap != IntPtr.Zero) DeleteObject(_hBitmap);
        }

        private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
            switch (uMsg)
            {
                case WM_PAINT:
                {
                    var hdc = BeginPaint(hWnd, out PAINTSTRUCT ps);

                    // Compose the whole frame off-screen, then blit once -> no flicker.
                    BitBlt(_backDC, 0, 0, _screenW, _screenH, _dimDC, 0, 0, SRCCOPY);

                    if (_isDrawing && Math.Abs(_endX - _startX) > 2 && Math.Abs(_endY - _startY) > 2)
                    {
                        int x = Math.Min(_startX, _endX);
                        int y = Math.Min(_startY, _endY);
                        int w = Math.Abs(_endX - _startX);
                        int h = Math.Abs(_endY - _startY);

                        // Reveal the selected region undimmed, then outline it.
                        BitBlt(_backDC, x, y, w, h, _memDC, x, y, SRCCOPY);

                        var pen = CreatePen(0, 2, 0x00ED8A64);
                        var nullBrush = GetStockObject(NULL_BRUSH);
                        var oldPen = SelectObject(_backDC, pen);
                        var oldBrush = SelectObject(_backDC, nullBrush);
                        Rectangle(_backDC, x, y, x + w, y + h);
                        SelectObject(_backDC, oldPen);
                        SelectObject(_backDC, oldBrush);
                        DeleteObject(pen);
                    }

                    BitBlt(hdc, 0, 0, _screenW, _screenH, _backDC, 0, 0, SRCCOPY);
                    EndPaint(hWnd, ref ps);
                    return IntPtr.Zero;
                }

                case WM_ERASEBKGND:
                    return (IntPtr)1; // handled; suppress default background erase

                case WM_LBUTTONDOWN:
                    _startX = _endX = (short)(lParam.ToInt64() & 0xFFFF);
                    _startY = _endY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    _isDrawing = true;
                    SetCapture(hWnd);
                    return IntPtr.Zero;

                case WM_MOUSEMOVE:
                    if (_isDrawing)
                    {
                        _endX = (short)(lParam.ToInt64() & 0xFFFF);
                        _endY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                        InvalidateRect(hWnd, IntPtr.Zero, false);
                    }
                    return IntPtr.Zero;

                case WM_LBUTTONUP:
                    if (_isDrawing)
                    {
                        _isDrawing = false;
                        ReleaseCapture();
                        _endX = (short)(lParam.ToInt64() & 0xFFFF);
                        _endY = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                        int rx = Math.Min(_startX, _endX);
                        int ry = Math.Min(_startY, _endY);
                        int rw = Math.Abs(_endX - _startX);
                        int rh = Math.Abs(_endY - _startY);
                        DestroyWindow(hWnd);
                        SelectionComplete?.Invoke(new System.Drawing.Rectangle(rx, ry, rw, rh));
                    }
                    return IntPtr.Zero;

                case WM_KEYDOWN:
                    if (wParam.ToInt32() == VK_ESCAPE)
                    {
                        DestroyWindow(hWnd);
                        SelectionComplete?.Invoke(System.Drawing.Rectangle.Empty);
                    }
                    return IntPtr.Zero;

                case WM_DESTROY:
                    PostQuitMessage(0);
                    return IntPtr.Zero;
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WndProc error: " + ex);
            }
            return DefWindowProc(hWnd, uMsg, wParam, lParam);
        }
    }
}