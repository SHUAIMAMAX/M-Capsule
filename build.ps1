param([switch]$Package)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$app=Join-Path $PSScriptRoot "build\sdk-$stamp"
& dotnet build src/M-Capsule.csproj -c Release -o $app
if($LASTEXITCODE -ne 0){throw 'Main build failed'}
if($Package){ & (Join-Path $PSScriptRoot 'package.ps1') -RuntimeDirectory $app }
Write-Output "Portable binaries: $app"
