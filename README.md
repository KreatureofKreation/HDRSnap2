# HDRSnap2

**Screenshots that don't wash out when Windows HDR is on.**

With HDR enabled, screenshots of everyday content — browsers, chats, documents, desktop apps — come out washed-out, milky, and over-bright. Even the Windows Snipping Tool gets this wrong: SDR content sitting in the HDR framebuffer isn't mapped back to SDR correctly. HDRSnap2 reads your display's actual SDR white level and tone-maps properly, so captures look exactly like your screen.

A lightweight tray app: hotkey → drag a box → done. Saves a PNG to `Pictures\HDRSnap` and copies it to the clipboard.

> ℹ️ **Where it matters:** the everyday **SDR-on-HDR** case — browsing, chatting, working — is what washes out. For *full-HDR game* content, modern Windows tools already capture correctly, and HDRSnap2 simply matches them there. The win is everything else.

---

## Features

- 🎨 **No more washed-out HDR shots** — captures the FP16 scRGB framebuffer and tone-maps scRGB → sRGB, normalized by your display's real SDR white level. Fixes the milky, over-bright look that SDR content (browsers, apps, chat) gets when HDR is on.
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
