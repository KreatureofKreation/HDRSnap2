Add-Type -AssemblyName System.Drawing
$proj = Split-Path $PSScriptRoot -Parent

function New-GlyphPng([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Match the runtime tray glyph exactly (AppHost.LoadTrayIcon): sharp orange
    # square + white crop-rectangle, using the same 32px geometry scaled to S.
    $f = $S / 32.0
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(232, 106, 51))
    $g.FillRectangle($brush, [int](2 * $f), [int](2 * $f), [int](28 * $f), [int](28 * $f))

    $pw = [float]([Math]::Max(1.0, 3 * $f))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $pw)
    $g.DrawRectangle($pen, [int](9 * $f), [int](9 * $f), [int](14 * $f), [int](14 * $f))

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $ms.ToArray()
}

$sizes = @(256, 64, 48, 32, 16)
$pngs = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($s in $sizes) { $pngs.Add((New-GlyphPng $s)) }

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $S = $sizes[$i]; $len = $pngs[$i].Length
    $b = if ($S -ge 256) { 0 } else { $S }
    $bw.Write([Byte]$b); $bw.Write([Byte]$b)
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
$icoPath = Join-Path $proj 'Assets\app.ico'
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
$bw.Dispose()

# verify it loads
$ic = New-Object System.Drawing.Icon($icoPath)
Write-Output ("wrote {0} ({1} bytes), loads as {2}x{3}" -f $icoPath, (Get-Item $icoPath).Length, $ic.Width, $ic.Height)
$ic.Dispose()
