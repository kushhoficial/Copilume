param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    & $DotNet run --project Windows/Tests/Copilume.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows tests failed.' }
    $preview = Join-Path $PSScriptRoot 'build/windows-preview'
    New-Item -ItemType Directory -Force $preview | Out-Null
    & $DotNet run --project Windows/Tests/Copilume.Tests.csproj -c Release --no-build -- --render (Join-Path $preview 'light.png')
    if ($LASTEXITCODE -ne 0) { throw 'Light UI smoke test failed.' }
    & $DotNet run --project Windows/Tests/Copilume.Tests.csproj -c Release --no-build -- --render (Join-Path $preview 'dark.png') --dark
    if ($LASTEXITCODE -ne 0) { throw 'Dark UI smoke test failed.' }
} finally { Pop-Location }
