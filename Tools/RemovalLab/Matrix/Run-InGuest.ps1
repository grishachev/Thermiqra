param([string]$ReportDirectory = 'C:\LabResults', [switch]$Resume)
$ErrorActionPreference = 'Stop'
$model = [string](Get-CimInstance Win32_ComputerSystem).Model
if ($model -notmatch '(?i)virtual machine|virtualbox|vmware|kvm|qemu|parallels') { throw 'Run only in a disposable virtual Windows.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $PSCommandPath + '"'),'-ReportDirectory',('"' + $ReportDirectory + '"'))
    if ($Resume) { $arguments += '-Resume' }
    $elevated = Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -Wait -PassThru -ArgumentList $arguments
    exit $elevated.ExitCode
}
$working = 'C:\ThermiqraDisposableLab'
if (-not $Resume) {
    if (Test-Path -LiteralPath $working) { throw 'Restore a clean guest snapshot before another run.' }
    New-Item -ItemType Directory -Path $working | Out-Null
    Copy-Item -Path (Join-Path $PSScriptRoot '*') -Destination $working -Recurse
}
New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
$env:THERMIQRA_DISPOSABLE_LAB = '1'
[IO.File]::WriteAllText((Join-Path $working 'guest-enabled.marker'), 'thermiqra-disposable-guest-v1')
$mode = if($Resume) { '--resume' } else { '--report' }
& (Join-Path $working 'Thermiqra.RemovalLab.Runner.exe') $mode (Join-Path $ReportDirectory 'matrix-results.json') *> (Join-Path $ReportDirectory ("matrix-output-$Resume.log"))
$result = $LASTEXITCODE
if ($result -eq 20) {
    # This branch is reachable only inside an opted-in virtual Windows guest.
    $runOnce = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce'
    $resumeCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $working 'Run-InGuest.ps1') + '" -Resume -ReportDirectory "' + $ReportDirectory + '"'
    New-ItemProperty -LiteralPath $runOnce -Name '!ThermiqraDisposableLabResume' -Value $resumeCommand -PropertyType String -Force | Out-Null
    Restart-Computer -Force
    exit 20
}
if ($result -ne 0) { Write-Output "Matrix did not pass. Report: $ReportDirectory\matrix-results.json" }
exit $result
