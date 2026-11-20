param([switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$sandbox = Join-Path $env:WINDIR 'System32\WindowsSandbox.exe'
if (-not $PrepareOnly -and -not (Test-Path -LiteralPath $sandbox)) { throw 'Windows Sandbox is not enabled. Enable Containers-DisposableClientVM as administrator and restart if Windows requests it.' }
$bundle = Join-Path $PSScriptRoot 'bin\Bundle'
if (-not (Test-Path -LiteralPath (Join-Path $bundle 'Thermiqra.RemovalLab.Runner.exe'))) { throw 'Build-Matrix.ps1 must complete first.' }
$results = Join-Path $PSScriptRoot ('bin\Results-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $results | Out-Null
$sourceXml = [Security.SecurityElement]::Escape($bundle)
$resultsXml = [Security.SecurityElement]::Escape($results)
$configuration = @"
<Configuration>
  <VGpu>Disable</VGpu><Networking>Disable</Networking><ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$sourceXml</HostFolder><SandboxFolder>C:\LabSource</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$resultsXml</HostFolder><SandboxFolder>C:\LabResults</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\LabSource\Run-InGuest.ps1 -ReportDirectory C:\LabResults</Command></LogonCommand>
</Configuration>
"@
$path = Join-Path $results 'ThermiqraLab.wsb'
[IO.File]::WriteAllText($path, $configuration, (New-Object Text.UTF8Encoding($true)))
if ($PrepareOnly) { Write-Output ('Prepared without launching: ' + $path); return }
# Keep the interactive guest visible for any required guest administrator consent.
Start-Process -FilePath $sandbox -ArgumentList ('"' + $path + '"') -WindowStyle Normal
Write-Output ('Guest results will appear in ' + $results)
