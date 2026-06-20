using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace HDRSnap2
{
    public class DxgiCapture : IDisposable
    {
        private SharpDX.Direct3D11.Device? _device;
        private Output1? _output;
        private OutputDuplication? _duplication;
        private int _width;
        private int _height;
        private int _offsetX;
        private int _offsetY;
        private float _sdrWhiteScale = 1.0f;

        public DxgiCapture()
        {
            Initialize();
        }

        private void Initialize()
        {
            _device = new SharpDX.Direct3D11.Device(
                SharpDX.Direct3D.DriverType.Hardware,
                DeviceCreationFlags.BgraSupport);

            using var dxgiDevice = _device.QueryInterface<SharpDX.DXGI.Device>();
            using var adapter = dxgiDevice.GetParent<Adapter>();
            var output = adapter.GetOutput(0);
            _output = output.QueryInterface<Output1>();

            var desc = _output.Description;
            _offsetX = desc.DesktopBounds.Left;
            _offsetY = desc.DesktopBounds.Top;
            _width = desc.DesktopBounds.Right - desc.DesktopBounds.Left;
            _height = desc.DesktopBounds.Bottom - desc.DesktopBounds.Top;

            // Request FP16 scRGB so we capture true HDR pixels. Plain DuplicateOutput()
            // always returns an 8-bit SDR projection that looks washed out on an HDR desktop.
            try
            {
                using var output5 = output.QueryInterface<Output5>();
                var formats = new[] { Format.R16G16B16A16_Float, Format.B8G8R8A8_UNorm };
                _duplication = output5.DuplicateOutput1(_device, 0, formats.Length, formats);
                System.Diagnostics.Debug.WriteLine("Using DuplicateOutput1 (HDR FP16)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DuplicateOutput1 failed, falling back to SDR: " + ex.Message);
                _duplication = _output.DuplicateOutput(_device);
            }

            output.Dispose();

            _sdrWhiteScale = HdrDisplay.GetSdrWhiteScale();
            System.Diagnostics.Debug.WriteLine($"SDR white scale: {_sdrWhiteScale}");
        }

        public async Task<byte[]?> CaptureRegionAsync(Windows.Foundation.Rect region)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var result = _duplication!.TryAcquireNextFrame(500,
                        out var frameInfo, out var desktopResource);

                    if (result.Failure || desktopResource == null)
                        return CaptureViaGdi(region);

                    using (desktopResource)
                    using (var texture = desktopResource.QueryInterface<SharpDX.Direct3D11.Texture2D>())
                    {
                        var texDesc = texture.Description;
                        texDesc.CpuAccessFlags = CpuAccessFlags.Read;
                        texDesc.Usage = ResourceUsage.Staging;
                        texDesc.OptionFlags = ResourceOptionFlags.None;
                        texDesc.BindFlags = BindFlags.None;

                        using var stagingTexture = new SharpDX.Direct3D11.Texture2D(_device!, texDesc);
                        _device!.ImmediateContext.CopyResource(texture, stagingTexture);
                        _duplication.ReleaseFrame();

                        var dataBox = _device.ImmediateContext.MapSubresource(
                            stagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                        int rx = Math.Max(0, (int)(region.X - _offsetX));
                        int ry = Math.Max(0, (int)(region.Y - _offsetY));
                        int rw = Math.Min((int)region.Width, _width - rx);
                        int rh = Math.Min((int)region.Height, _height - ry);

                        if (rw <= 0 || rh <= 0)
                        {
                            _device.ImmediateContext.UnmapSubresource(stagingTexture, 0);
                            return null;
                        }

                        int srcStride = dataBox.RowPitch;
                        var fmt = texDesc.Format;
                        System.Diagnostics.Debug.WriteLine($"Capture format: {fmt}, RowPitch: {srcStride}");

                        byte[] pixels = ToBgra8(dataBox.DataPointer, srcStride, rx, ry, rw, rh, fmt, _sdrWhiteScale);

                        _device.ImmediateContext.UnmapSubresource(stagingTexture, 0);

                        _lastWidth = rw;
                        _lastHeight = rh;
                        return pixels;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("DXGI error: " + ex.Message);
                    return CaptureViaGdi(region);
                }
            });
        }

        public int _lastWidth;
        public int _lastHeight;

        // Full desktop, HDR-corrected — used for the selector preview background.
        public Task<byte[]?> CaptureFullScreenAsync()
            => CaptureRegionAsync(new Windows.Foundation.Rect(_offsetX, _offsetY, _width, _height));

        private byte[]? CaptureViaGdi(Windows.Foundation.Rect region)
        {
            try
            {
                int x = (int)region.X;
                int y = (int)region.Y;
                int w = (int)region.Width;
                int h = (int)region.Height;
                if (w <= 0 || h <= 0) return null;

                var bmp = new System.Drawing.Bitmap(w, h,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));

                var bmpData = bmp.LockBits(
                    new System.Drawing.Rectangle(0, 0, w, h),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                byte[] pixels = new byte[w * h * 4];
                Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);
                bmp.UnlockBits(bmpData);

                _lastWidth = w;
                _lastHeight = h;
                return pixels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GDI error: " + ex.Message);
                return null;
            }
        }

        // Convert a captured DXGI surface region to 8-bit BGRA (sRGB), handling HDR formats.
        private static byte[] ToBgra8(IntPtr basePtr, int srcStride, int rx, int ry, int rw, int rh, Format fmt, float whiteScale)
        {
            byte[] dst = new byte[rw * rh * 4];
            int bpp = fmt == Format.R16G16B16A16_Float ? 8 : 4;
            byte[] row = new byte[rw * bpp];
            float inv = whiteScale > 0f ? 1f / whiteScale : 1f;

            for (int y = 0; y < rh; y++)
            {
                IntPtr srcRow = basePtr + (ry + y) * srcStride + rx * bpp;
                Marshal.Copy(srcRow, row, 0, row.Length);
                int d = y * rw * 4;

                for (int x = 0; x < rw; x++)
                {
                    int s = x * bpp;
                    byte b, g, r;

                    switch (fmt)
                    {
                        case Format.R16G16B16A16_Float: // scRGB linear; normalize so SDR white -> 1.0
                            r = ScRgbToSrgb((float)BitConverter.ToHalf(row, s) * inv);
                            g = ScRgbToSrgb((float)BitConverter.ToHalf(row, s + 2) * inv);
                            b = ScRgbToSrgb((float)BitConverter.ToHalf(row, s + 4) * inv);
                            break;

                        case Format.R10G10B10A2_UNorm: // HDR10: PQ-encoded (gamut xform approximated)
                        {
                            uint p = BitConverter.ToUInt32(row, s);
                            r = ScRgbToSrgb(Pq((p & 0x3FF) / 1023f) * inv);
                            g = ScRgbToSrgb(Pq(((p >> 10) & 0x3FF) / 1023f) * inv);
                            b = ScRgbToSrgb(Pq(((p >> 20) & 0x3FF) / 1023f) * inv);
                            break;
                        }

                        case Format.R8G8B8A8_UNorm:
                            r = row[s]; g = row[s + 1]; b = row[s + 2];
                            break;

                        default: // B8G8R8A8_UNorm (already SDR sRGB)
                            b = row[s]; g = row[s + 1]; r = row[s + 2];
                            break;
                    }

                    dst[d + x * 4 + 0] = b;
                    dst[d + x * 4 + 1] = g;
                    dst[d + x * 4 + 2] = r;
                    dst[d + x * 4 + 3] = 255;
                }
            }
            return dst;
        }

        // scRGB linear (Rec.709) -> 8-bit sRGB. Highlights >1.0 clip to white.
        private static byte ScRgbToSrgb(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 1f) return 255;
            float s = v <= 0.0031308f ? v * 12.92f : 1.055f * MathF.Pow(v, 1f / 2.4f) - 0.055f;
            int i = (int)(s * 255f + 0.5f);
            return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
        }

        // SMPTE ST 2084 (PQ) decode -> scRGB-normalized linear (1.0 = 80 nits).
        private static float Pq(float e)
        {
            const float m1 = 0.1593017578125f, m2 = 78.84375f;
            const float c1 = 0.8359375f, c2 = 18.8515625f, c3 = 18.6875f;
            float ep = MathF.Pow(e, 1f / m2);
            float num = MathF.Max(ep - c1, 0f);
            float den = c2 - c3 * ep;
            float l = MathF.Pow(num / den, 1f / m1); // [0,1], 1.0 = 10000 nits
            return l * 10000f / 80f;
        }

        public void Dispose()
        {
            _duplication?.Dispose();
            _output?.Dispose();
            _device?.Dispose();
        }
    }
}