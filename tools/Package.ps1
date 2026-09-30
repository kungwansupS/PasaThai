param(
    [Parameter(Mandatory = $true)][string]$TML,
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'ThaiOfflineAllMods'
if (!$Out) { $Out = Join-Path $root '.qa' }
$TML = (Resolve-Path -LiteralPath $TML).Path
if (!(Test-Path -LiteralPath (Join-Path $TML 'tMLMod.targets')) -or
    !(Test-Path -LiteralPath (Join-Path $TML 'tModLoader.dll'))) {
    throw 'TML must point to a tModLoader installation with tMLMod.targets and tModLoader.dll.'
}

& dotnet build (Join-Path $source 'ThaiOfflineAllMods.csproj') -c Release "-p:TMLInstallPath=$TML" '-p:BuildMod=false' --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Compile failed.' }

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path -LiteralPath $Out).Path
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('ThaiOfflineAllModsPackage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$runner = Join-Path $TML 'dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $runner)) { $runner = 'dotnet' }
$log = Join-Path $Out '_package.log'

$savedPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
Push-Location $TML
try {
    & $runner (Join-Path $TML 'tModLoader.dll') -server -build $source `
        -eac (Join-Path $source 'bin/Release/net8.0/ThaiOfflineAllMods.dll') `
        -savedirectory $scratch -tmlsavedirectory $scratch -modpath (Join-Path $scratch 'Mods') *> $log
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
    $ErrorActionPreference = $savedPreference
}
$built = Join-Path $scratch 'Mods/ThaiOfflineAllMods.tmod'
if ($code -ne 0 -or !(Test-Path -LiteralPath $built)) { throw "Packaging failed. See $log" }
$target = Join-Path $Out 'ThaiOfflineAllMods.tmod'
Copy-Item -LiteralPath $built -Destination $target -Force
Get-FileHash -LiteralPath $target -Algorithm SHA256 | Select-Object Path, Hash
