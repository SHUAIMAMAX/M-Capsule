param([string]$RuntimeDirectory=(Join-Path $PSScriptRoot 'build\app'),[string]$Destination=(Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference='Stop'
$stage=Join-Path $PSScriptRoot ('build\package-'+[guid]::NewGuid().ToString('N'))
$source=Join-Path $stage 'source'
$runtime=Join-Path $stage 'MS-Beatuty-Capsule-Portable-0.6.5'
New-Item -ItemType Directory -Force -Path $source,$runtime,$Destination | Out-Null
function Copy-SourceTree([string]$From,[string]$To) {
    New-Item -ItemType Directory -Force -Path $To | Out-Null
    foreach($item in Get-ChildItem -LiteralPath $From) {
        if($item.PSIsContainer) {
            if($item.Name -notin @('bin','obj','build','Data','SandboxData')) { Copy-SourceTree $item.FullName (Join-Path $To $item.Name) }
        } else { Copy-Item -LiteralPath $item.FullName -Destination $To }
    }
}
foreach($name in @('src','tests','vendor')) { Copy-SourceTree (Join-Path $PSScriptRoot $name) (Join-Path $source $name) }
foreach($name in @('build.ps1','build-local.ps1','package.ps1','create-app-icon.ps1','BUILD.txt','使用说明.txt','LICENSE.txt','THIRD-PARTY-NOTICES.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $source }
$sourceZip=Join-Path $Destination 'MS-Beatuty-Capsule-Portable-0.6.5-Source.zip'
if(Test-Path -LiteralPath $sourceZip){throw 'Source output exists; choose a new destination.'}
Compress-Archive -Path (Join-Path $source '*') -DestinationPath $sourceZip
foreach($name in @('MSBeatutyCapsule.exe','MSBeatutyCapsule.exe.config')) { Copy-Item -LiteralPath (Join-Path $RuntimeDirectory $name) -Destination $runtime }
New-Item -ItemType Directory (Join-Path $runtime 'vendor') | Out-Null
foreach($name in @('ExplorerTAP.dll','Detours-LICENSE.txt','WIL-LICENSE.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot "vendor\$name") -Destination (Join-Path $runtime 'vendor') }
foreach($name in @('使用说明.txt','LICENSE.txt','THIRD-PARTY-NOTICES.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $runtime }
Copy-Item -LiteralPath $sourceZip -Destination $runtime
$report=Join-Path $Destination '修改与测试报告-0.6.5.md'
if(Test-Path -LiteralPath $report) { Copy-Item -LiteralPath $report -Destination $runtime }
$zip=Join-Path $Destination 'MS-Beatuty-Capsule-Portable-0.6.5.zip'
if(Test-Path -LiteralPath $zip){throw 'Portable output exists; choose a new destination.'}
Compress-Archive -LiteralPath $runtime -DestinationPath $zip
Write-Output $sourceZip
Write-Output $zip

