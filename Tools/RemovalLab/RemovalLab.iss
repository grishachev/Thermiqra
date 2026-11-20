[Setup]
AppId=ThermiqraRemovalLab
AppName=Thermiqra Removal Lab
AppVersion=1.0
AppPublisher=ThermiqraLab
DefaultDirName={localappdata}\Programs\ThermiqraRemovalLab
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\ThermiqraRemovalLab.exe
OutputDir=bin\Installer
OutputBaseFilename=ThermiqraRemovalLab_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bin\ThermiqraRemovalLab.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Thermiqra Removal Lab"; Filename: "{app}\ThermiqraRemovalLab.exe"

[Run]
Filename: "{app}\ThermiqraRemovalLab.exe"; Parameters: "--seed"; Flags: runhidden waituntilterminated; StatusMsg: "Создание контрольных данных для проверки удаления..."; AfterInstall: VerifySeed

[Code]
procedure VerifySeed();
begin
  if not FileExists(ExpandConstant('{localappdata}\ThermiqraLabVerification\manifest.json')) then
    RaiseException('Не удалось создать контрольные данные. Этот прогон нельзя считать установленным образцом.');
end;

function InitializeSetup(): Boolean;
begin
  Result := not (DirExists(ExpandConstant('{localappdata}\Programs\ThermiqraRemovalLab')) or
    DirExists(ExpandConstant('{localappdata}\ThermiqraRemovalLab')) or
    DirExists(ExpandConstant('{userappdata}\ThermiqraRemovalLab')) or
    DirExists(ExpandConstant('{localappdata}\ThermiqraLabNeighbor')) or
    DirExists(ExpandConstant('{localappdata}\ThermiqraLabVerification')) or
    RegKeyExists(HKCU, 'Software\ThermiqraLab\ThermiqraRemovalLab') or
    RegKeyExists(HKCU, 'Software\ThermiqraLab\Neighbor'));
  if not Result then MsgBox('Контрольные пути уже заняты. Восстановите чистый снимок тестовой Windows перед повторной установкой.', mbError, MB_OK);
end;
