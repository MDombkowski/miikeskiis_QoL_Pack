# Draws icon.png, the package icon (256 x 256 pixels): a folded map with a dashed route leading to a glowing portal.
# Rebuild it with:
#   powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1 [-OutFile <path>]
# It uses System.Drawing, which comes with Windows PowerShell, and writes only the icon file (by default icon.png
# in the mod folder).
param([string]$OutFile = (Join-Path (Split-Path -Parent $PSScriptRoot) 'icon.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$made = New-Object 'System.Collections.Generic.List[System.IDisposable]'
function Keep($disposable) { $made.Add($disposable); return $disposable }

function Colour([string]$hex, [int]$alpha = 255) {
    [System.Drawing.Color]::FromArgb($alpha, [System.Drawing.ColorTranslator]::FromHtml($hex))
}

function Points([float[]]$xy) {
    $points = New-Object 'System.Drawing.PointF[]' ($xy.Length / 2)
    for ($i = 0; $i -lt $points.Length; $i++) { $points[$i] = New-Object System.Drawing.PointF($xy[2 * $i], $xy[2 * $i + 1]) }
    return ,$points
}

function RoundPen([string]$hex, [float]$width, [int]$alpha = 255) {
    $pen = Keep (New-Object System.Drawing.Pen((Colour $hex $alpha), $width))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function EllipseBrush([float]$x, [float]$y, [float]$width, [float]$height, $centre, $edge) {
    $path = Keep (New-Object System.Drawing.Drawing2D.GraphicsPath)
    $path.AddEllipse($x, $y, $width, $height)
    $brush = Keep (New-Object System.Drawing.Drawing2D.PathGradientBrush($path))
    $brush.CenterColor = $centre
    $brush.SurroundColors = [System.Drawing.Color[]]@($edge)
    return @{ Path = $path; Brush = $brush }
}

$size = 256
$bitmap = Keep (New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
$g = Keep ([System.Drawing.Graphics]::FromImage($bitmap))
try {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    # Background: dark slate, lighter at the top.
    $canvas = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $g.FillRectangle((Keep (New-Object System.Drawing.Drawing2D.LinearGradientBrush($canvas, (Colour '#33465A'), (Colour '#0F151D'), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical))), $canvas)

    # The folded map: three panels of parchment, the middle one in shade.
    $mapOutline = RoundPen '#4A3A22' 3
    foreach ($panel in @(
            @{ Corners = @(34, 78, 96, 62, 96, 200, 34, 216);    Fill = '#EBDDB5' },
            @{ Corners = @(96, 62, 160, 78, 160, 216, 96, 200);  Fill = '#CFBB8A' },
            @{ Corners = @(160, 78, 222, 62, 222, 200, 160, 216); Fill = '#EBDDB5' })) {
        $corners = Points $panel.Corners
        $g.FillPolygon((Keep (New-Object System.Drawing.SolidBrush((Colour $panel.Fill)))), $corners)
        $g.DrawPolygon($mapOutline, $corners)
    }

    # The route: a dashed red line from a start mark to the portal.
    $routeColour = '#A32E1E'
    $route = RoundPen $routeColour 4
    $route.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Custom
    $route.DashPattern = [float[]]@(2.2, 1.7)
    $route.DashCap = [System.Drawing.Drawing2D.DashCap]::Round
    $g.DrawBezier($route, 60, 184, 86, 178, 104, 140, 138, 132)
    $g.FillEllipse((Keep (New-Object System.Drawing.SolidBrush((Colour $routeColour)))), 52, 176, 16, 16)

    # The portal, centred on (170, 112): a soft glow, a swirling surface and a stone ring.
    $glow = EllipseBrush 116 40 108 144 (Colour '#8CFFF0' 150) (Colour '#5A3CDC' 0)
    $g.FillPath($glow.Brush, $glow.Path)
    $surface = EllipseBrush 143 72 54 80 (Colour '#DCFFFA') (Colour '#5B3CD2')
    $g.FillPath($surface.Brush, $surface.Path)
    $swirl = RoundPen '#FFFFFF' 3 170
    $g.DrawArc($swirl, 151, 84, 38, 52, 200, 210)
    $g.DrawArc($swirl, 161, 97, 18, 28, 20, 230)
    $g.DrawEllipse((RoundPen '#3A4048' 10), 140, 69, 60, 86)
    $g.DrawEllipse((RoundPen '#8E97A2' 2), 134.5, 63.5, 71, 97)

    $bitmap.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
    "Wrote $OutFile"
}
finally {
    for ($i = $made.Count - 1; $i -ge 0; $i--) { $made[$i].Dispose() }
}
