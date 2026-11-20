param([ValidateSet('Installed', 'Native', 'Cleaned')][string]$Stage = 'Cleaned')
$ErrorActionPreference = 'Stop'
$local = [Environment]::GetFolderPath('LocalApplicationData')
$roaming = [Environment]::GetFolderPath('ApplicationData')
$control = Join-Path $local 'ThermiqraLabVerification'
$manifestPath = Join-Path $control 'manifest.json'

function Assert-NoLink([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        try {
            if (([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Link refused: $cursor" }
        } catch [IO.FileNotFoundException] { } catch [IO.DirectoryNotFoundException] { }
        $parent = [IO.Directory]::GetParent($cursor)
        $cursor = if ($null -eq $parent) { $null } else { $parent.FullName }
    }
}
Assert-NoLink $manifestPath
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Контрольный манифест отсутствует. Установите образец в тестовой Windows.' }
if ((Get-Item -LiteralPath $manifestPath).Length -gt 1MB) { throw 'Manifest exceeds limit.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$roots = @((Join-Path $local 'Programs\ThermiqraRemovalLab'), (Join-Path $roaming 'ThermiqraRemovalLab'), (Join-Path $local 'ThermiqraRemovalLab'))
$neighborFile = Join-Path $local 'ThermiqraLabNeighbor\keep.txt'
$ownKey = 'Software\ThermiqraLab\ThermiqraRemovalLab'
$neighborKey = 'Software\ThermiqraLab\Neighbor'
$uninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\ThermiqraRemovalLab_is1'
$runId = [Guid]::Empty
if ($manifest.Schema -ne 1 -or -not [Guid]::TryParseExact($manifest.RunId, 'N', [ref]$runId) -or
    $manifest.InstallRoot -ne $roots[0] -or $manifest.RegistryKey -ne $ownKey -or $manifest.NeighborKey -ne $neighborKey -or
    $manifest.UninstallKey -ne $uninstallKey -or @($manifest.Files).Count -ne 66) { throw 'Invalid test manifest.' }

$checks = New-Object 'System.Collections.Generic.List[object]'
function Check([string]$Item, [bool]$Passed, [string]$Detail) {
    $checks.Add([pscustomobject]@{ Item = $Item; Passed = $Passed; Detail = $Detail })
}
$seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$executable = Join-Path $roots[0] 'ThermiqraRemovalLab.exe'
Assert-NoLink $executable
Check $executable ((Test-Path -LiteralPath $executable -PathType Leaf) -eq ($Stage -eq 'Installed')) 'Проверка установленного EXE'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Thermiqra Removal Lab.lnk'
Assert-NoLink $shortcut
Check $shortcut ((Test-Path -LiteralPath $shortcut -PathType Leaf) -eq ($Stage -eq 'Installed')) 'Проверка ярлыка меню «Пуск»'
foreach ($file in $manifest.Files) {
    $path = [IO.Path]::GetFullPath([string]$file.Path)
    $inScope = $false
    foreach ($root in $roots) { if ($path.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { $inScope = $true } }
    if (($file.Preserve -and $path -ne $neighborFile) -or (-not $file.Preserve -and -not $inScope) -or
        $file.Sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or -not $seen.Add($path)) { throw 'Unexpected fixture path or hash.' }
    Assert-NoLink $path
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    if ($file.Preserve -or $Stage -ne 'Cleaned') {
        $fileMatches = $exists -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $file.Sha256
        Check $path $fileMatches 'Должен сохраниться без изменений'
    } else { Check $path (-not $exists) 'Остаток должен отсутствовать' }
}
$registrationCount = 0
foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)) {
    $user = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, $view)
    try {
        $key = $user.OpenSubKey($ownKey)
        try {
            if ($Stage -eq 'Cleaned') { Check "$view $ownKey" ($null -eq $key) 'Настройки образца должны отсутствовать' }
            else {
                $valid = $null -ne $key -and $key.GetValue('RunId') -eq $manifest.RunId -and $key.GetValue('Number') -eq 42 -and
                    $key.GetValueKind('Number') -eq [Microsoft.Win32.RegistryValueKind]::DWord -and
                    $key.GetValueKind('Items') -eq [Microsoft.Win32.RegistryValueKind]::MultiString -and
                    (@($key.GetValue('Items')) -join ',') -eq 'first,second' -and
                    ([BitConverter]::ToString($key.GetValue('Bytes'))) -eq '00-11-FF'
                Check "$view $ownKey" $valid 'Настройки и типы данных должны сохраниться'
            }
        } finally { if ($key) { $key.Dispose() } }
        $other = $user.OpenSubKey($neighborKey)
        try { Check "$view $neighborKey" ($null -ne $other -and $other.GetValue('RunId') -eq $manifest.RunId) 'Соседний продукт нельзя затрагивать' }
        finally { if ($other) { $other.Dispose() } }
        $entry = $user.OpenSubKey($uninstallKey)
        try {
            if ($null -ne $entry) { $registrationCount++ }
            if ($Stage -ne 'Installed') { Check "$view uninstall" ($null -eq $entry) 'Штатная регистрация должна отсутствовать' }
        }
        finally { if ($entry) { $entry.Dispose() } }
    } finally { $user.Dispose() }
}
if ($Stage -eq 'Installed') { Check 'Windows uninstall registration' ($registrationCount -gt 0) 'Штатная регистрация должна существовать' }
if ($Stage -eq 'Cleaned') {
    foreach ($root in $roots) { Assert-NoLink $root; Check $root (-not (Test-Path -LiteralPath $root)) 'Включая пустые папки и неучтённые файлы' }
}
$failed = @($checks | Where-Object { -not $_.Passed })
$report = [pscustomobject]@{ Stage = $Stage; RunId = $manifest.RunId; CheckedUtc = [DateTime]::UtcNow.ToString('O');
    Passed = $failed.Count -eq 0; Checks = $checks.Count; Failures = $failed; Details = $checks }
$reportPath = Join-Path $control ("verification-$Stage.json")
Assert-NoLink $reportPath
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Output ("Этап: $Stage. Проверок: $($checks.Count). Ошибок: $($failed.Count).")
if ($failed.Count) { $failed | Format-Table -AutoSize; exit 1 }
Write-Output 'Контрольные данные соответствуют ожидаемому результату.'
