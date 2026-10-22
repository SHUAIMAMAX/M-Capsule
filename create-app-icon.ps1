param(
    [string]$InputPng=(Join-Path $PSScriptRoot 'src\app-sunglasses.png'),
    [string]$OutputIco=(Join-Path $PSScriptRoot 'src\app.ico')
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[System.Drawing.Image]::FromFile($InputPng)
try {
    $images=foreach($size in @(16,24,32,48,64,128,256)) {
        $bitmap=[System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                # Trim the generated transparent margins so the sunglasses read at tray size.
                $crop=[System.Drawing.Rectangle]::new(120,120,$source.Width-240,$source.Height-240)
                $graphics.DrawImage($source,[System.Drawing.Rectangle]::new(0,0,$size,$size),$crop,[System.Drawing.GraphicsUnit]::Pixel)
            } finally {$graphics.Dispose()}
            $stream=[System.IO.MemoryStream]::new()
            try {$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png); [pscustomobject]@{Size=$size;Bytes=$stream.ToArray()}}
            finally {$stream.Dispose()}
        } finally {$bitmap.Dispose()}
    }
    $stream=[System.IO.File]::Create($OutputIco)
    try {
        $writer=[System.IO.BinaryWriter]::new($stream)
        try {
            $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$images.Count)
            $offset=6+16*$images.Count
            foreach($entry in $images) {
                $writer.Write([byte]($entry.Size % 256));$writer.Write([byte]($entry.Size % 256))
                $writer.Write([byte]0);$writer.Write([byte]0)
                $writer.Write([uint16]1);$writer.Write([uint16]32)
                $writer.Write([uint32]$entry.Bytes.Length);$writer.Write([uint32]$offset)
                $offset+=$entry.Bytes.Length
            }
            foreach($entry in $images) {$writer.Write([byte[]]$entry.Bytes)}
        } finally {$writer.Dispose()}
    } finally {$stream.Dispose()}
} finally {$source.Dispose()}
