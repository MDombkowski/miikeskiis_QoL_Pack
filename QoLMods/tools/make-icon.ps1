# Draws icon.png, the bundle's package icon (256 x 256 pixels): a round wooden Viking shield with an iron rim and boss,
# and a banner across it reading "QoL".
# Rebuild it with:
#   powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1 [-OutFile <path>]
# It uses System.Drawing, which comes with Windows PowerShell, and writes only the icon file (by default icon.png in the
# bundle's folder).
param([string]$OutFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'icon.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$made = New-Object 'System.Collections.Generic.List[System.IDisposable]'
function Keep($disposable) { $made.Add($disposable); return $disposable }

function Colour([string]$hex, [int]$alpha = 255) {
    [System.Drawing.Color]::FromArgb($alpha, [System.Drawing.ColorTranslator]::FromHtml($hex))
}

function Brush([string]$hex, [int]$alpha = 255) { Keep (New-Object System.Drawing.SolidBrush((Colour $hex $alpha))) }

function Pen([string]$hex, [float]$width, [int]$alpha = 255) {
    $pen = Keep (New-Object System.Drawing.Pen((Colour $hex $alpha), $width))
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

$size = 256
$bitmap = Keep (New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
$g = Keep ([System.Drawing.Graphics]::FromImage($bitmap))
try {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Background: dark slate, lighter at the top (the same as XPortal Map Picker's icon, so they read as one family).
    $canvas = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $g.FillRectangle((Keep (New-Object System.Drawing.Drawing2D.LinearGradientBrush($canvas, (Colour '#33465A'), (Colour '#0F151D'), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical))), $canvas)

    # The shield: wooden planks in two tones, clipped to a circle centred on (128, 118), radius 96.
    $cx = 128; $cy = 118; $r = 96
    $shield = Keep (New-Object System.Drawing.Drawing2D.GraphicsPath)
    $shield.AddEllipse($cx - $r, $cy - $r, 2 * $r, 2 * $r)
    $g.SetClip($shield)
    $plank = 32
    for ($i = 0; $i -lt 7; $i++) {
        $x = $cx - $r + $i * $plank
        $tone = if ($i % 2 -eq 0) { '#9C6B3C' } else { '#86592F' }
        $g.FillRectangle((Brush $tone), $x, $cy - $r, $plank, 2 * $r)
        $g.DrawLine((Pen '#4E3219' 2), $x, $cy - $r, $x, $cy + $r)
    }
    # A painted band from top to bottom, as on many Norse shields.
    $g.FillRectangle((Brush '#A32E1E' 200), $cx - 22, $cy - $r, 44, 2 * $r)
    $g.ResetClip()

    # The iron rim, and the boss in the middle.
    $g.DrawEllipse((Pen '#3A4048' 12), $cx - $r + 4, $cy - $r + 4, 2 * $r - 8, 2 * $r - 8)
    $g.DrawEllipse((Pen '#8E97A2' 2), $cx - $r - 2, $cy - $r - 2, 2 * $r + 4, 2 * $r + 4)
    $bossPath = Keep (New-Object System.Drawing.Drawing2D.GraphicsPath)
    $bossPath.AddEllipse($cx - 24, $cy - 24, 48, 48)
    $boss = Keep (New-Object System.Drawing.Drawing2D.PathGradientBrush($bossPath))
    $boss.CenterPoint = New-Object System.Drawing.PointF(($cx - 8), ($cy - 8))
    $boss.CenterColor = Colour '#D8DEE4'
    $boss.SurroundColors = [System.Drawing.Color[]]@((Colour '#4A5058'))
    $g.FillPath($boss, $bossPath)
    $g.DrawEllipse((Pen '#2A2E34' 2), $cx - 24, $cy - 24, 48, 48)

    # The banner: a parchment ribbon across the lower shield with "QoL" on it.
    $ribbon = Keep (New-Object System.Drawing.Drawing2D.GraphicsPath)
    $ribbon.AddPolygon([System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF(18, 184)), (New-Object System.Drawing.PointF(238, 184)),
            (New-Object System.Drawing.PointF(226, 208)), (New-Object System.Drawing.PointF(238, 232)),
            (New-Object System.Drawing.PointF(18, 232)), (New-Object System.Drawing.PointF(30, 208))))
    $g.FillPath((Brush '#EBDDB5'), $ribbon)
    $g.DrawPath((Pen '#4A3A22' 3), $ribbon)
    $font = Keep (New-Object System.Drawing.Font('Georgia', 34, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel))
    $centred = Keep (New-Object System.Drawing.StringFormat)
    $centred.Alignment = [System.Drawing.StringAlignment]::Center
    $centred.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString('QoL', $font, (Brush '#3A2A14'), (New-Object System.Drawing.RectangleF(18, 184, 220, 50)), $centred)

    $bitmap.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
    "Wrote $OutFile"
}
finally {
    for ($i = $made.Count - 1; $i -ge 0; $i--) { $made[$i].Dispose() }
}
