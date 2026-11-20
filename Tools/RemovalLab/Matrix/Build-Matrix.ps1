param([string]$Wix)
$ErrorActionPreference = 'Stop'
$matrixRoot = $PSScriptRoot
$bundle = Join-Path $matrixRoot 'bin\Bundle'
$packages = Join-Path $bundle 'Packages'
New-Item -ItemType Directory -Path $packages -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:exe /platform:x64 /optimize+ "/out:$bundle\MatrixSubject.exe" /reference:System.Management.dll /reference:System.Web.Extensions.dll (Join-Path $matrixRoot 'Subject\MatrixSubject.cs')
if ($LASTEXITCODE -ne 0) { throw 'Subject build failed.' }
$resumeDirectory = Join-Path $bundle 'AA-Resume'
New-Item -ItemType Directory -Path $resumeDirectory -Force | Out-Null
& $csc /nologo /target:winexe /platform:x64 /optimize+ "/out:$resumeDirectory\MatrixResume.exe" /reference:System.Management.dll (Join-Path $matrixRoot 'Subject\MatrixResume.cs')
if ($LASTEXITCODE -ne 0) { throw 'Guest continuation build failed.' }
$actions = Join-Path $matrixRoot 'bin\Actions'
dotnet build (Join-Path $matrixRoot 'MsiActions\MsiActions.csproj') -c Debug '-p:Platform=x64' "-p:OutDir=$actions\"
if ($LASTEXITCODE -ne 0) { throw 'MSI action build failed.' }
if (-not $Wix) { $Wix = Join-Path $matrixRoot '..\bin\wix\wix.exe' }
if (-not (Test-Path -LiteralPath $Wix)) {
    if ($PSBoundParameters.ContainsKey('Wix')) { throw 'Specified WiX compiler is unavailable.' }
    dotnet tool install wix --version 5.0.2 --tool-path (Split-Path -Parent $Wix)
    if ($LASTEXITCODE -ne 0) { throw 'Pinned WiX build tool restore failed.' }
}
if ((& $Wix --version) -notlike '5.0.2*') { throw 'Unexpected WiX tool version.' }
$subjectXml = [Security.SecurityElement]::Escape((Join-Path $bundle 'MatrixSubject.exe'))
$actionXml = [Security.SecurityElement]::Escape((Join-Path $actions 'ThermiqraMatrixActions.CA.dll'))
foreach ($id in @('MsiUser','MsiMachine','MsiCancel','MsiError','MsiReboot')) {
    $scope = if($id -eq 'MsiUser') { 'perUser' } else { 'perMachine' }
    $registryRoot = if($scope -eq 'perUser') { 'HKCU' } else { 'HKLM' }
    $removeParent = if($scope -eq 'perUser') { '<RemoveFolder Id="RemoveLabPrograms" Directory="LabPrograms" On="uninstall" />' } else { '' }
    $directories = if($scope -eq 'perUser') { '<StandardDirectory Id="LocalAppDataFolder"><Directory Id="LabPrograms" Name="Programs"><Directory Id="INSTALLFOLDER" Name="ThermiqraMatrixMsiUser" /></Directory></StandardDirectory>' }
        else { '<StandardDirectory Id="ProgramFiles64Folder"><Directory Id="INSTALLFOLDER" Name="ThermiqraMatrix' + $id + '" /></StandardDirectory>' }
    $failureAction = if($id -eq 'MsiCancel') { 'CancelRemoval' } elseif($id -eq 'MsiError') { 'FailRemoval' } else { '' }
    $failureDefinition = if($failureAction) { '<CustomAction Id="ControlledFailure" BinaryRef="LabActions" DllEntry="' + $failureAction + '" Return="check" />' } else { '' }
    $failureSequence = if($failureAction) { '<Custom Action="ControlledFailure" Before="RemoveFiles" Condition="REMOVE = &quot;ALL&quot;" />' } else { '' }
    $rebootSequence = if($id -eq 'MsiReboot') { '<ScheduleReboot Before="InstallFinalize" Condition="REMOVE = &quot;ALL&quot;" />' } else { '' }
    $wxs = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="ThermiqraMatrix$id" Manufacturer="ThermiqraLab" Version="1.0.0" Scope="$scope" UpgradeCode="$([Guid]::NewGuid())" InstallerVersion="500">
    <MediaTemplate EmbedCab="yes" />
    <Property Id="ARPNOMODIFY" Value="1" />
    <Property Id="REBOOT" Value="ReallySuppress" />
    <Property Id="LAB_LOCAL" Secure="yes" /><Property Id="LAB_ROAMING" Secure="yes" /><Property Id="LAB_SID" Secure="yes" />
    <SetProperty Id="ARPINSTALLLOCATION" Value="[INSTALLFOLDER]" After="CostFinalize" />
    $directories
    <DirectoryRef Id="INSTALLFOLDER">
      <Component Id="MainComponent" Guid="$([Guid]::NewGuid())">
        <File Id="SubjectExe" Source="$subjectXml" />
        <RemoveFolder Id="RemoveInstallFolder" On="uninstall" />
        $removeParent
        <RegistryValue Root="$registryRoot" Key="Software\ThermiqraMatrixInstallRecords\ThermiqraMatrix$id" Name="Installed" Type="integer" Value="1" KeyPath="yes" />
        <RemoveRegistryKey Root="$registryRoot" Key="Software\ThermiqraMatrixInstallRecords\ThermiqraMatrix$id" Action="removeOnUninstall" />
      </Component>
    </DirectoryRef>
    <Feature Id="MainFeature"><ComponentRef Id="MainComponent" /></Feature>
    <Binary Id="LabActions" SourceFile="$actionXml" />
    $failureDefinition
    <CustomAction Id="Seed" FileRef="SubjectExe" ExeCommand="--seed $id &quot;[LAB_LOCAL]&quot; &quot;[LAB_ROAMING]&quot; &quot;[LAB_SID]&quot;" Return="check" />
    <InstallExecuteSequence>
      <Custom Action="Seed" After="InstallFinalize" Condition="NOT Installed" />
      $failureSequence
      $rebootSequence
    </InstallExecuteSequence>
  </Package>
</Wix>
"@
    $source = Join-Path $packages ($id + '.wxs')
    [IO.File]::WriteAllText($source, $wxs, (New-Object Text.UTF8Encoding($true)))
    & $Wix build -arch x64 $source -o (Join-Path $packages ($id + '.msi'))
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed: $id" }
}
dotnet build (Join-Path $matrixRoot 'Runner\Runner.csproj') -c Debug -r win-x64 --self-contained true "-p:OutDir=$bundle\" -p:CopyRetryCount=0
if ($LASTEXITCODE -ne 0) { throw 'Self-contained runner build failed.' }
Copy-Item -LiteralPath (Join-Path $matrixRoot 'Run-InGuest.ps1') -Destination $bundle -Force
Copy-Item -LiteralPath (Join-Path $matrixRoot 'THIRD_PARTY.md') -Destination $bundle -Force
Write-Output ('Built without installation: ' + $bundle)
