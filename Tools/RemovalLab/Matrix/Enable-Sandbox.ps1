$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $process = Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -Wait -PassThru -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $PSCommandPath + '"'))
    exit $process.ExitCode
}
$statusDirectory = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $statusDirectory -Force | Out-Null
$statusFile = Join-Path $statusDirectory 'sandbox-feature-state.json'
try {
    $state = Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All -NoRestart
    [pscustomobject]@{ Status='Enabled'; RestartNeeded=[bool]$state.RestartNeeded; CheckedUtc=[DateTime]::UtcNow.ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath $statusFile -Encoding UTF8
    $state | Select-Object FeatureName,State,RestartNeeded | Format-List
    if ($state.RestartNeeded) { Write-Output 'Windows requires a restart. This script does not restart the host.' }
    else { Write-Output 'Sandbox enabled. Start-Sandbox.ps1 can launch the prepared regression run.' }
} catch {
    [pscustomobject]@{ Status='Failed'; Detail=$_.Exception.Message; CheckedUtc=[DateTime]::UtcNow.ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath $statusFile -Encoding UTF8
    throw
}
