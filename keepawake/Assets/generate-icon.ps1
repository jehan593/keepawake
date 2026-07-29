Add-Type -AssemblyName System.Drawing

# Same palette and background treatment as dnsw's tray icon (Nord0 square, Nord8/Nord3 glyph swap for
# on/off) — this app has no window to theme, so the icon is the one visual surface worth keeping
# consistent with dnsw's look.
$bgColor = [System.Drawing.Color]::FromArgb(255, 0x2E, 0x34, 0x40)   # nord0

function New-RoundedRectPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$size, [System.Drawing.Color]$fgColor) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded-square background (~22% corner radius), matching dnsw's squircle look — the canvas
    # outside it stays fully transparent so the tray/taskbar's own background shows through.
    $bgBrush = New-Object System.Drawing.SolidBrush $bgColor
    $bgPath = New-RoundedRectPath 0 0 $size $size ($size * 0.22)
    $g.FillPath($bgBrush, $bgPath)
    $bgPath.Dispose()

    # Monitor glyph in a 108x108 viewport: bezel + inset screen (cut back to the background color, so
    # it reads as a dark screen framed by the bezel, not a solid blob) + a small neck/base — matches
    # what this app actually does (keeps the display on). Same proportions at every size so the icon
    # reads as one consistent asset rather than hand-tuned per resolution.
    $scale = $size / 108.0
    $brush = New-Object System.Drawing.SolidBrush $fgColor

    # Bezel.
    $bezelPath = New-RoundedRectPath (18 * $scale) (20 * $scale) (72 * $scale) (52 * $scale) (8 * $scale)
    $g.FillPath($brush, $bezelPath)
    $bezelPath.Dispose()

    # Screen: inset from the bezel, punched out in the background color.
    $screenPath = New-RoundedRectPath (25 * $scale) (27 * $scale) (58 * $scale) (38 * $scale) (4 * $scale)
    $g.FillPath($bgBrush, $screenPath)
    $screenPath.Dispose()
    $bgBrush.Dispose()

    # Neck + base stand.
    $neckPath = New-RoundedRectPath (48 * $scale) (72 * $scale) (12 * $scale) (14 * $scale) (2 * $scale)
    $g.FillPath($brush, $neckPath)
    $neckPath.Dispose()

    $basePath = New-RoundedRectPath (34 * $scale) (86 * $scale) (40 * $scale) (8 * $scale) (3 * $scale)
    $g.FillPath($brush, $basePath)
    $basePath.Dispose()

    $brush.Dispose()
    $g.Dispose()
    return $bmp
}

function Write-Ico([string]$path, [System.Drawing.Color]$fgColor) {
    $sizes = @(16, 32, 48, 256)
    $pngBytesBySize = @{}
    foreach ($s in $sizes) {
        $bmp = New-IconBitmap $s $fgColor
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytesBySize[$s] = $ms.ToArray()
        $bmp.Dispose()
    }

    $out = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $out
    $bw.Write([UInt16]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]$sizes.Count)

    $headerSize = 6 + (16 * $sizes.Count)
    $offset = $headerSize
    foreach ($s in $sizes) {
        $data = $pngBytesBySize[$s]
        $dim = if ($s -ge 256) { 0 } else { $s }
        $bw.Write([Byte]$dim)
        $bw.Write([Byte]$dim)
        $bw.Write([Byte]0)
        $bw.Write([Byte]0)
        $bw.Write([UInt16]1)
        $bw.Write([UInt16]32)
        $bw.Write([UInt32]$data.Length)
        $bw.Write([UInt32]$offset)
        $offset += $data.Length
    }
    foreach ($s in $sizes) {
        $bw.Write($pngBytesBySize[$s])
    }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($path, $out.ToArray())
    Write-Output "Wrote $path"
}

Write-Ico "$PSScriptRoot\app-on.ico" ([System.Drawing.Color]::FromArgb(255, 0x88, 0xC0, 0xD0))   # nord8
Write-Ico "$PSScriptRoot\app-off.ico" ([System.Drawing.Color]::FromArgb(255, 0x4C, 0x56, 0x6A))  # nord3
