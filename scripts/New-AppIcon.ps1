# Zeichnet das Programm-Icon (Karteikarten auf grünem "chunky" Grund, Farben wie
# in Styles/AppStyles.xaml) und schreibt src/EnglishSteamTrainer.UI/app.ico sowie
# das Icon der Pflege-Oberfläche (icon-192.png).
# Kleine Größen bekommen eine vereinfachte Zeichnung, damit sie lesbar bleiben.
# Braucht WPF, also Windows PowerShell im STA-Modus:
#   powershell.exe -NoProfile -STA -File scripts\New-AppIcon.ps1
param(
    [string]$OutFile = (Join-Path $PSScriptRoot "..\src\EnglishSteamTrainer.UI\app.ico"),
    [string]$WebIconFile = (Join-Path $PSScriptRoot "..\src\EnglishSteamTrainer.Admin\wwwroot\icon-192.png"),
    [string]$PreviewDir
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

function Brush([string]$hex) { [Windows.Media.BrushConverter]::new().ConvertFromString($hex) }

function Draw-Card($dc, [double]$cx, [double]$cy, [double]$w, [double]$h, [double]$angle, $fill, [double]$radius) {
    $dc.PushTransform([Windows.Media.RotateTransform]::new($angle, $cx, $cy))
    $dc.DrawRoundedRectangle($fill, $null, [Windows.Rect]::new($cx - $w / 2, $cy - $h / 2, $w, $h), $radius, $radius)
    $dc.Pop()
}

function Star-Geometry([double]$cx, [double]$cy, [double]$outer, [double]$inner) {
    $points = for ($i = 0; $i -lt 10; $i++) {
        $r = if ($i % 2 -eq 0) { $outer } else { $inner }
        $a = [Math]::PI * (-90 + 36 * $i) / 180
        [Windows.Point]::new($cx + $r * [Math]::Cos($a), $cy + $r * [Math]::Sin($a))
    }
    $figure = [Windows.Media.PathFigure]::new()
    $figure.StartPoint = $points[0]
    $figure.IsClosed = $true
    foreach ($p in $points[1..9]) { $figure.Segments.Add([Windows.Media.LineSegment]::new($p, $true)) }
    $geometry = [Windows.Media.PathGeometry]::new()
    $geometry.Figures.Add($figure)
    $geometry
}

# Zeichnet auf eine 256er-Fläche, skaliert auf $size.
function Render-Icon([int]$size) {
    $simple = $size -le 24
    $visual = [Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $dc.PushTransform([Windows.Media.ScaleTransform]::new($size / 256, $size / 256))

    # Grund mit dunklerer Unterkante wie bei den Buttons.
    $inset = if ($simple) { 4 } else { 10 }
    $box = 256 - 2 * $inset
    $dc.DrawRoundedRectangle((Brush "#46A302"), $null, [Windows.Rect]::new($inset, $inset, $box, $box), 52, 52)
    $top = [Windows.Media.LinearGradientBrush]::new((Brush "#7BDE2A").Color, (Brush "#58CC02").Color, 90)
    $dc.DrawRoundedRectangle($top, $null, [Windows.Rect]::new($inset, $inset, $box, $box - 16), 52, 52)

    if ($simple) {
        Draw-Card $dc 116 122 124 156 -14 (Brush "#1CB0F6") 20
        Draw-Card $dc 138 122 124 156 8 (Brush "#FFFFFF") 20
    }
    else {
        Draw-Card $dc 118 124 128 158 -13 (Brush "#1CB0F6") 18
        Draw-Card $dc 134 130 132 160 7 (Brush "#2E7D0A") 18
        Draw-Card $dc 134 124 132 160 7 (Brush "#FFFFFF") 18

        $dc.PushTransform([Windows.Media.RotateTransform]::new(7, 134, 124))
        $face = [Windows.Media.Typeface]::new([Windows.Media.FontFamily]::new("Segoe UI"),
            [Windows.FontStyles]::Normal, [Windows.FontWeights]::Black, [Windows.FontStretches]::Normal)
        $text = [Windows.Media.FormattedText]::new("Aa", [Globalization.CultureInfo]::InvariantCulture,
            [Windows.FlowDirection]::LeftToRight, $face, 72, (Brush "#263238"), 1.0)
        $dc.DrawText($text, [Windows.Point]::new(134 - $text.Width / 2, 118 - $text.Height / 2))
        $dc.DrawRoundedRectangle((Brush "#1CB0F6"), $null, [Windows.Rect]::new(100, 160, 68, 10), 5, 5)
        $dc.Pop()

        $dc.DrawGeometry((Brush "#E0A800"), $null, (Star-Geometry 196 196 46 21))
        $dc.DrawGeometry((Brush "#FFC800"), $null, (Star-Geometry 196 192 46 21))
    }

    $dc.Pop()
    $dc.Close()

    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    $encoder.Save($stream)
    , $stream.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($size in $sizes) { , (Render-Icon $size) }

if ($PreviewDir) {
    New-Item -ItemType Directory -Force $PreviewDir | Out-Null
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        [IO.File]::WriteAllBytes((Join-Path $PreviewDir "icon-$($sizes[$i]).png"), $images[$i])
    }
}

# ICO-Datei mit PNG-Einträgen (seit Windows Vista unterstützt).
$out = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($out)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32)
    $writer.Write([UInt32]$images[$i].Length); $writer.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()
[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($OutFile), $out.ToArray())
"Icon geschrieben: $([IO.Path]::GetFullPath($OutFile))"

[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($WebIconFile), (Render-Icon 192))
"Icon geschrieben: $([IO.Path]::GetFullPath($WebIconFile))"
