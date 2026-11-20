param([string]$Compiler)
$ErrorActionPreference = 'Stop'
$labRoot = $PSScriptRoot
$output = Join-Path $labRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw 'Windows .NET Framework compiler is unavailable.' }
& $csc /nologo /target:winexe /optimize+ "/out:$output\ThermiqraRemovalLab.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $labRoot 'RemovalLab.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test application compilation failed.' }
if (-not $Compiler) {
    $candidates = @((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'))
    $Compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Compiler) { throw 'Specify the installed Inno Setup compiler using -Compiler.' }
& $Compiler (Join-Path $labRoot 'RemovalLab.iss')
if ($LASTEXITCODE -ne 0) { throw 'Test installer compilation failed.' }
Write-Output ('Prepared without installation: ' + (Join-Path $output 'Installer\ThermiqraRemovalLab_Setup.exe'))
