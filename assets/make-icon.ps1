<#
    Generates assets\phone-debug.ico - a simple placeholder mark for Phone Debug.
    Run it only when the icon needs to be regenerated; the .ico is committed.

        powershell -ExecutionPolicy Bypass -File assets\make-icon.ps1
#>
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$outFile = Join-Path $PSScriptRoot "phone-debug.ico"
$sizes = @(16, 24, 32, 48, 64, 128, 256)

$accent = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)   # blue
$screen = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$dot = [System.Drawing.Color]::FromArgb(255, 34, 197, 94)      # green "connected" dot

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
    $path.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
    $path.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

$pngs = @()
foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [single]$size
    $bodyW = $s * 0.56
    $bodyH = $s * 0.86
    $bodyX = ($s - $bodyW) / 2
    $bodyY = ($s - $bodyH) / 2

    $body = New-RoundedPath $bodyX $bodyY $bodyW $bodyH ([Math]::Max(1.5, $s * 0.12))
    $brush = New-Object System.Drawing.SolidBrush($accent)
    $g.FillPath($brush, $body)
    $brush.Dispose()
    $body.Dispose()

    $pad = [Math]::Max(1.0, $s * 0.07)
    $inner = New-RoundedPath ($bodyX + $pad) ($bodyY + $pad * 1.6) ($bodyW - $pad * 2) ($bodyH - $pad * 3.4) ([Math]::Max(0.5, $s * 0.05))
    $brush = New-Object System.Drawing.SolidBrush($screen)
    $g.FillPath($brush, $inner)
    $brush.Dispose()
    $inner.Dispose()

    if ($size -ge 32) {
        $r = $s * 0.11
        $brush = New-Object System.Drawing.SolidBrush($dot)
        $g.FillEllipse($brush, ($bodyX + $bodyW - $r * 0.8), ($bodyY + $bodyH - $r * 1.5), $r, $r)
        $brush.Dispose()
    }

    $g.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += , @{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose()
}

# ICO container: header + one directory entry per image + the PNG payloads.
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($out)
$writer.Write([UInt16]0)               # reserved
$writer.Write([UInt16]1)               # type: icon
$writer.Write([UInt16]$pngs.Count)

$offset = 6 + (16 * $pngs.Count)
foreach ($png in $pngs) {
    $dim = if ($png.Size -ge 256) { 0 } else { $png.Size }
    $writer.Write([Byte]$dim)          # width
    $writer.Write([Byte]$dim)          # height
    $writer.Write([Byte]0)             # palette
    $writer.Write([Byte]0)             # reserved
    $writer.Write([UInt16]1)           # colour planes
    $writer.Write([UInt16]32)          # bits per pixel
    $writer.Write([UInt32]$png.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $png.Bytes.Length
}

foreach ($png in $pngs) {
    $writer.Write($png.Bytes)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($outFile, $out.ToArray())
$writer.Dispose()
$out.Dispose()

Write-Host "Wrote $outFile ($((Get-Item $outFile).Length) bytes)"
