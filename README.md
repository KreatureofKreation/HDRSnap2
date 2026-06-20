# HDRSnap2

**HDR-accurate screenshots for Windows.**

Most screenshot tools capture a washed-out, faded image when your display is in HDR mode. HDRSnap2 grabs the *real* HDR pixels and tone-maps them correctly, so the saved image looks like what you actually see — on HDR **and** SDR displays.

A lightweight tray app: press a hotkey, drag a box, done. Saves a PNG and copies it to the clipboard.

---

## Features

- 🎨 **HDR-accurate** — captures the FP16 scRGB framebuffer and tone-maps scRGB → sRGB, normalized by the real SDR white level. No more milky, washed-out HDR screenshots.
- ⚡ **Hotkey capture** — `Ctrl+Alt+Q` by default, freezes the screen, drag-to-select (WYSIWYG — what you freeze is what you save).
- 🖱️ **System tray** — capture, open folder, change hotkey, start-with-Windows toggle, exit.
- ⌨️ **Custom hotkey** — pick your own combo in Settings; remembered between runs.
- 📋 Saves to `Pictures\HDRSnap` **and** the clipboard.
- 📦 **Zero dependencies** — self-contained, runs on any clean Windows 10 (1809+) / 11 x64.

## Install

Grab the latest `HDRSnap2.zip` from [Releases](../../releases), extract, and run `HDRSnap2.exe`.

> ⚠️ The app isn't code-signed, so Windows SmartScreen may warn "Windows protected your PC." Click **More info → Run anyway**. (Or build it yourself — see below.)

## How it works

| Stage | What happens |
|-------|--------------|
| Capture | `IDXGIOutput5::DuplicateOutput1` requesting `R16G16B16A16_FLOAT` — the true HDR compositor surface (plain `DuplicateOutput` only gives a washed 8-bit SDR projection). |
| Tone-map | scRGB linear → sRGB, normalized by the SDR white level from `DisplayConfigGetDeviceInfo`. Highlights above SDR white clip to white. |
| Overlay | Pure Win32 fullscreen window, double-buffered, shows the frozen HDR-correct frame to drag over. |
| Output | Crop the frozen frame → PNG (`Pictures\HDRSnap`) + clipboard. |

## Build from source

Requires the .NET 8 SDK and the Windows App SDK workload.

```powershell
# Debug (run via Visual Studio F5 for the packaged debug experience)
dotnet build HDRSnap2.csproj -p:Platform=x64 -c Debug

# Self-contained release folder
dotnet publish HDRSnap2.csproj -c Release -p:Platform=x64 -r win-x64 `
  --self-contained true -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true `
  -p:PublishTrimmed=false -p:PublishReadyToRun=false -o publish
```

`tools/dotnet-launcher/` is a tiny .NET launcher used to keep the distributed layout clean (`HDRSnap2.exe` + `README.txt` on top, runtime DLLs in `app\`). `tools/gen-icon.ps1` generates `Assets/app.ico`.

## Tech

C# / .NET 8 / WinUI 3, with native Windows APIs via P/Invoke. DXGI capture via SharpDX. No C++ in the shipped app.

## License

MIT — see [LICENSE](LICENSE).
