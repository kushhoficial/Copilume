param(
    [ValidateSet('x64', 'arm64')][string]$Architecture = 'x64',
    [string]$DotNet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    $version = (Get-Content -LiteralPath VERSION -Raw).Trim()
    $buildNumber = (Get-Content -LiteralPath BUILD_NUMBER -Raw).Trim()
    if ($version -notmatch '^\d+\.\d+\.\d+$' -or $buildNumber -notmatch '^\d+$') { throw 'Invalid version.' }
    $output = Join-Path $PSScriptRoot "build/windows/win-$Architecture"
    New-Item -ItemType Directory -Force $output | Out-Null
    # Multi-resolution ICO generated from the official brand asset, without external tools.
    Add-Type -AssemblyName System.Drawing
    $source = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'Assets/AppIcon.png'))
    $iconPath = Join-Path $PSScriptRoot 'build/windows/Copilume.ico'
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $images = [System.Collections.Generic.List[byte[]]]::new()
    try {
        foreach ($size in $sizes) {
            $bitmap = [System.Drawing.Bitmap]::new($size, $size)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            $stream = [System.IO.MemoryStream]::new()
            try {
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($source, 0, 0, $size, $size)
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $images.Add($stream.ToArray())
            } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
        }
    } finally { $source.Dispose() }
    $writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($iconPath))
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([uint16]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
            $offset += $images[$i].Length
        }
        foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
    } finally { $writer.Dispose() }
    & $DotNet publish Windows/Copilume.Windows.csproj -c Release -r "win-$Architecture" --self-contained true -o $output "-p:Version=$version" "-p:FileVersion=$version.$buildNumber" "-p:ApplicationIcon=$iconPath" -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    Copy-Item -LiteralPath LICENSE -Destination $output
    Copy-Item -LiteralPath docs/WINDOWS.md -Destination (Join-Path $output 'LEIA-ME.md')
    Write-Host "Windows app: $output/Copilume.exe"
} finally { Pop-Location }
