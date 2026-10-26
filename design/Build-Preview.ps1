$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase, System.Drawing
$designRoot = $PSScriptRoot
$renderPath = Join-Path $designRoot 'png'
New-Item -ItemType Directory -Path $renderPath -Force | Out-Null

function Convert-FluentIcon([string]$SourcePath, [int]$Size, [string]$Color) {
    [xml]$svg = [IO.File]::ReadAllText($SourcePath)
    $viewBox = $svg.DocumentElement.GetAttribute('viewBox').Split(' ')
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.PushTransform((New-Object System.Windows.Media.ScaleTransform(($Size / [double]$viewBox[2]), ($Size / [double]$viewBox[3]))))
    $brush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString($Color))
    foreach ($node in $svg.SelectNodes("//*[local-name()='path']")) {
        $geometry = [System.Windows.Media.Geometry]::Parse($node.GetAttribute('d')).Clone()
        $geometry.FillRule = [System.Windows.Media.FillRule]::Nonzero
        if ($node.GetAttribute('fill-rule') -eq 'evenodd') { $geometry.FillRule = [System.Windows.Media.FillRule]::EvenOdd }
        $context.DrawGeometry($brush, $null, $geometry)
    }
    $context.Pop(); $context.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($Size, $Size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    $bytes = $stream.ToArray(); $stream.Dispose()
    return ,$bytes
}

function Write-FluentIco([string]$Name, [string]$SourceName, [string]$Color) {
    $source = Join-Path $designRoot ('fluent-icons\' + $SourceName)
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
    $frames = @{}
    foreach ($size in $sizes) {
        $frames[$size] = Convert-FluentIcon $source $size $Color
        [IO.File]::WriteAllBytes((Join-Path $renderPath ($Name + '-' + $size + '.png')), $frames[$size])
    }
    $stream = [IO.File]::Create((Join-Path $designRoot ($Name + '.ico')))
    $writer = New-Object System.IO.BinaryWriter($stream)
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    foreach ($size in $sizes) {
        $dimension = $size
        if ($size -eq 256) { $dimension = 0 }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$size].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$size].Length
    }
    foreach ($size in $sizes) { $writer.Write([byte[]]$frames[$size]) }
    $writer.Dispose()
}

Write-FluentIco 'RefreshSentinel-Fluent' 'ic_fluent_desktop_sync_24_filled.svg' '#0078D4'
Write-FluentIco 'RefreshSentinel-Check' 'ic_fluent_desktop_checkmark_24_filled.svg' '#0078D4'
Write-FluentIco 'RefreshSentinel-Pulse' 'ic_fluent_desktop_pulse_24_filled.svg' '#0078D4'
Write-FluentIco 'tray-light' 'ic_fluent_desktop_sync_20_regular.svg' '#202020'
Write-FluentIco 'tray-dark' 'ic_fluent_desktop_sync_20_regular.svg' '#FFFFFF'

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $designRoot 'fluent-icons') -Filter '*_20_regular.svg') {
    [IO.File]::WriteAllBytes((Join-Path $renderPath ($file.BaseName + '.png')), (Convert-FluentIcon $file.FullName 40 '#202020'))
    [IO.File]::WriteAllBytes((Join-Path $renderPath ($file.BaseName + '-white.png')), (Convert-FluentIcon $file.FullName 40 '#FFFFFF'))
}

function Color([string]$hex) { return [Drawing.ColorTranslator]::FromHtml($hex) }
function Text([string]$value, [single]$size, [single]$x, [single]$y, [string]$hex, [bool]$bold = $false) {
    $family = 'Segoe UI'
    if ($bold) { $family = 'Segoe UI Semibold' }
    $font = New-Object Drawing.Font($family, $size, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object Drawing.SolidBrush((Color $hex))
    $graphics.DrawString($value, $font, $brush, $x, $y)
    $brush.Dispose(); $font.Dispose()
}
function Rounded([single]$x, [single]$y, [single]$width, [single]$height, [single]$radius, [string]$fill, [string]$border = '') {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90); $path.AddArc(($x + $width - $d), $y, $d, $d, 270, 90)
    $path.AddArc(($x + $width - $d), ($y + $height - $d), $d, $d, 0, 90); $path.AddArc($x, ($y + $height - $d), $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object Drawing.SolidBrush((Color $fill)); $graphics.FillPath($brush, $path); $brush.Dispose()
    if ($border) { $pen = New-Object Drawing.Pen((Color $border), 1); $graphics.DrawPath($pen, $path); $pen.Dispose() }
    $path.Dispose()
}
function Icon([string]$file, [single]$x, [single]$y, [single]$size) {
    $image = [Drawing.Image]::FromFile((Join-Path $renderPath $file))
    $graphics.DrawImage($image, $x, $y, $size, $size); $image.Dispose()
}

$preview = New-Object Drawing.Bitmap(1200, 790)
$graphics = [Drawing.Graphics]::FromImage($preview)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$graphics.Clear((Color '#F3F3F3'))
Text 'RefreshSentinel' 30 34 23 '#1A1A1A' $true
Text 'Windows 11 / Fluent ikon- és megjelenési válogatás' 16 35 65 '#616161'

$options = @(
    @{ name='Desktop Sync'; asset='RefreshSentinel-Fluent'; subtitle='Monitor + frissítés'; note='Javasolt alkalmazásikon' },
    @{ name='Desktop Checkmark'; asset='RefreshSentinel-Check'; subtitle='Monitor + helyes állapot'; note='Alternatíva a felügyelethez' },
    @{ name='Desktop Pulse'; asset='RefreshSentinel-Pulse'; subtitle='Monitor + megfigyelés'; note='Alternatíva a Hz figyeléséhez' }
)
for ($index = 0; $index -lt $options.Count; $index++) {
    $x = 36 + $index * 380
    Rounded $x 112 368 190 8 '#FFFFFF' '#E5E5E5'
    Icon ($options[$index].asset + '-96.png') ($x + 18) 135 72
    Text $options[$index].name 19 ($x + 106) 139 '#1A1A1A' $true
    Text $options[$index].subtitle 14 ($x + 106) 170 '#616161'
    Text $options[$index].note 14 ($x + 22) 226 '#0067C0' $true
    Icon ($options[$index].asset + '-16.png') ($x + 24) 265 16
    Icon ($options[$index].asset + '-24.png') ($x + 56) 261 24
    Icon ($options[$index].asset + '-32.png') ($x + 98) 257 32
    Text '16 / 24 / 32 px' 12 ($x + 151) 265 '#616161'
}

Text 'Felületi irány' 22 34 326 '#1A1A1A' $true
Text 'Világos és sötét téma · letisztult beállítássorok · Segoe UI · finom kiemelőszín' 14 35 359 '#616161'
foreach ($dark in @($false, $true)) {
    $x = 36; $bg = '#F9F9F9'; $surface = '#FFFFFF'; $ink = '#1A1A1A'; $muted = '#616161'; $line = '#E5E5E5'; $accent = '#0067C0'; $onAccent = '#FFFFFF'; $heading = 'Világos téma'
    if ($dark) { $x = 616; $bg = '#202020'; $surface = '#2B2B2B'; $ink = '#FFFFFF'; $muted = '#C5C5C5'; $line = '#3A3A3A'; $accent = '#60CDFF'; $onAccent = '#003E56'; $heading = 'Sötét téma' }
    Rounded $x 397 548 210 8 $bg $line
    Text $heading 20 ($x + 20) 415 $ink $true
    Rounded ($x + 20) 459 508 70 8 $surface $line
    Text 'Monitor 1' 14 ($x + 36) 471 $ink $true
    Text 'Aktuális képfrissítés: 144 Hz' 12 ($x + 36) 495 $muted
    Rounded ($x + 363) 475 127 36 4 $surface $line
    Text '144 Hz' 14 ($x + 380) 483 $ink
    Text '⌄' 15 ($x + 469) 480 $ink
    Rounded ($x + 20) 546 107 36 4 $accent
    Text 'Mentés' 14 ($x + 49) 555 $onAccent $true
    Text 'Felügyelet aktív' 14 ($x + 149) 555 $muted
}

Text 'Egységes gombikonok' 19 34 631 '#1A1A1A' $true
$commands = @(
    @{ key='save'; label='Mentés' }, @{ key='arrow_sync'; label='Frissítés' }, @{ key='settings'; label='Beállítások' },
    @{ key='pause'; label='Szünet' }, @{ key='play'; label='Folytatás' }, @{ key='document_text'; label='Napló' }
)
for ($index = 0; $index -lt $commands.Count; $index++) {
    $x = 39 + $index * 191
    Icon ('ic_fluent_' + $commands[$index].key + '_20_regular.png') $x 673 24
    Text $commands[$index].label 14 ($x + 35) 675 '#1A1A1A'
}
Text 'Ikonok: Microsoft Fluent UI System Icons / MIT. A felületi minta megjelenési javaslat.' 12 35 743 '#616161'
$preview.Save((Join-Path $designRoot 'preview.png'), [Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose(); $preview.Dispose()
Write-Output 'Elkészült: 5 többméretes ICO, PNG-változatok és preview.png.'
