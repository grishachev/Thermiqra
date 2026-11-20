param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [string]$KeyDirectory = (Join-Path $env:USERPROFILE '.thermiqra-signing')
)
$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$name = [IO.Path]::GetFileName($installerPath)
if ($name -notmatch '^Thermiqra_Setup_(\d+\.\d+\.\d+(?:\.\d+)?)\.exe$') { throw 'Unexpected installer filename.' }
$releaseVersion = $Matches[1]
dotnet run --project (Join-Path $PSScriptRoot 'ReleaseSigning.csproj') -- sign $KeyDirectory $installerPath $releaseVersion
if ($LASTEXITCODE -ne 0) { throw 'Release signing failed; do not publish this package.' }
dotnet run --project (Join-Path $PSScriptRoot 'ReleaseSigning.csproj') -- verify-release $installerPath $releaseVersion
if ($LASTEXITCODE -ne 0) { throw 'Release signature verification failed.' }
Write-Output 'Ready: upload the installer, its .signature.json and .sha256 files to the same GitHub release.'
