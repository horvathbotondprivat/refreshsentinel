param([switch]$SkipTests, [switch]$SkipLiveChecks)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = $PSScriptRoot
$releasePath = Join-Path $projectRoot 'dist'
$verificationPath = Join-Path $projectRoot 'verification'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'A Windows .NET Framework C# fordítója nem található.' }
New-Item -ItemType Directory -Path $releasePath, $verificationPath -Force | Out-Null

# Build a small, native icon with no external dependencies.
$iconPath = Join-Path $projectRoot 'assets\app.ico'
if (-not (Test-Path -LiteralPath $iconPath)) {
    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap(64, 64)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(18, 21, 29))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(152, 135, 255), 4)
    $greenPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(107, 224, 178), 3)
    $graphics.DrawRectangle($pen, 9, 12, 46, 32)
    $graphics.DrawLine($pen, 32, 45, 32, 52)
    $graphics.DrawLine($pen, 23, 53, 41, 53)
    $points = [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(15, 29)), (New-Object System.Drawing.Point(22, 29)),
        (New-Object System.Drawing.Point(27, 21)), (New-Object System.Drawing.Point(34, 36)),
        (New-Object System.Drawing.Point(40, 26)), (New-Object System.Drawing.Point(49, 26))
    )
    $graphics.DrawLines($greenPen, $points)
    $png = New-Object System.IO.MemoryStream
    $bitmap.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $png.ToArray()
    $file = [System.IO.File]::Create($iconPath)
    $writer = New-Object System.IO.BinaryWriter($file)
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$pngBytes.Length); $writer.Write([uint32]22)
    $writer.Write($pngBytes)
    $writer.Dispose(); $png.Dispose(); $greenPen.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}

$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$commonOptions = @('/nologo', '/utf8output', '/codepage:65001', '/langversion:5', '/warn:4', '/warnaserror+', '/optimize+', '/platform:anycpu', '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Runtime.Serialization.dll')
$executablePath = Join-Path $releasePath 'RefreshSentinel.exe'
& $compilerPath @commonOptions '/target:winexe' ('/out:' + $executablePath) ('/win32icon:' + $iconPath) ('/resource:' + $iconPath + ',RefreshSentinel.App.ico') ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')) @sources
if ($LASTEXITCODE -ne 0) { throw 'Az alkalmazás fordítása sikertelen.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'app.config') -Destination ($executablePath + '.config') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.hu.md') -Destination (Join-Path $releasePath 'Hasznalat.txt') -Force

if (-not $SkipTests) {
    $testExecutable = Join-Path $verificationPath 'RefreshSentinel.Tests.exe'
    & $compilerPath @commonOptions '/target:exe' '/main:RefreshSentinel.Tests.TestRunner' ('/out:' + $testExecutable) ('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')) @sources (Join-Path $projectRoot 'tests\TestRunner.cs')
    if ($LASTEXITCODE -ne 0) { throw 'A tesztek fordítása sikertelen.' }
    $testArguments = @($verificationPath)
    if ($SkipLiveChecks) { $testArguments += '--skip-live' }
    & $testExecutable @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'A tesztelés hibát talált; a csomag nem készült el.' }
}

$packageFiles = @($executablePath, ($executablePath + '.config'), (Join-Path $releasePath 'Hasznalat.txt'))
Compress-Archive -LiteralPath $packageFiles -DestinationPath (Join-Path $projectRoot 'RefreshSentinel-Windows.zip') -Force
Get-FileHash -LiteralPath $executablePath -Algorithm SHA256 | Format-List
Write-Output ('Elkészült: ' + $executablePath)
