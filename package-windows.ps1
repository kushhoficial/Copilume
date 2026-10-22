param(
    [ValidateSet('x64', 'arm64')][string]$Architecture = 'x64',
    [string]$DotNet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    $version = (Get-Content -LiteralPath VERSION -Raw).Trim()
    $destination = Join-Path $PSScriptRoot "dist/Copilume-$version-windows-$Architecture.zip"
    if (Test-Path -LiteralPath $destination) { throw "Refusing to replace existing package: $destination" }
    & ./build-windows.ps1 -Architecture $Architecture -DotNet $DotNet
    New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'dist') | Out-Null
    Compress-Archive -Path (Join-Path $PSScriptRoot "build/windows/win-$Architecture/*") -DestinationPath $destination
    $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([System.IO.Path]::GetFileName($destination))" | Set-Content -LiteralPath (Join-Path $PSScriptRoot "dist/SHA256SUMS-windows-$Architecture.txt") -Encoding ascii
    Write-Host "Package: $destination"
} finally { Pop-Location }
