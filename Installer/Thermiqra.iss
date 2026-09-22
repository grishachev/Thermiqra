#define MyAppName "Thermiqra"
#define MyAppVersion "0.5.0"
#define MyAppPublisher "Thermiqra"
#define MyAppExeName "Thermiqra.exe"

[Setup]
AppId={{8E09F24C-EBAB-4D7A-A41E-B324B10E8E17}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

DefaultDirName={autopf}\Thermiqra
DefaultGroupName=Thermiqra

DisableProgramGroupPage=yes
DisableWelcomePage=no

; Установщик всегда получает права администратора
PrivilegesRequired=admin

OutputDir=Output
OutputBaseFilename=Thermiqra_Setup_0.5.0

SetupIconFile=..\PCHardwareMonitor\Assets\Thermiqra.ico

Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no

UninstallDisplayName=Thermiqra
UninstallDisplayIcon={app}\Thermiqra.exe

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]

Name: "desktopicon"; \
    Description: "Создать значок на рабочем столе"; \
    GroupDescription: "Дополнительные значки:"; \
    Flags: unchecked

[Files]

; ============================================================
; THERMIQRA
; ============================================================

Source: "..\PCHardwareMonitor\bin\Release\net10.0-windows\win-x64\publish\*"; \
    DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs


; ============================================================
; PAWNIO
; ============================================================

Source: "Dependencies\PawnIO_setup.exe"; \
    DestDir: "{tmp}"; \
    Flags: deleteafterinstall


[Icons]

; Меню Пуск
Name: "{group}\Thermiqra"; \
    Filename: "{app}\Thermiqra.exe"

; Рабочий стол
Name: "{autodesktop}\Thermiqra"; \
    Filename: "{app}\Thermiqra.exe"; \
    Tasks: desktopicon


[Run]

; ============================================================
; УСТАНОВКА / ОБНОВЛЕНИЕ PAWNIO
; ============================================================

Filename: "{tmp}\PawnIO_setup.exe"; \
    Parameters: "-install -silent"; \
    StatusMsg: "Установка системного драйвера PawnIO..."; \
    Flags: waituntilterminated runhidden


; ============================================================
; ЗАПУСК THERMIQRA ПОСЛЕ УСТАНОВКИ
; ============================================================

Filename: "{app}\Thermiqra.exe"; \
    Description: "Запустить Thermiqra"; \
    Flags: nowait postinstall skipifsilent runascurrentuser


[UninstallDelete]

Type: filesandordirs; \
    Name: "{app}"


[Code]

var
  RemoveUserData: Boolean;


function IsThermiqraRunning: Boolean;
begin
  Result :=
    CheckForMutexes(
      'Local\PCHardwareMonitor_SingleInstance');
end;


procedure CloseThermiqra;
var
  ResultCode: Integer;
  AppExe: String;
begin
  if not IsThermiqraRunning then
    Exit;

  AppExe :=
    ExpandConstant(
      '{app}\{#MyAppExeName}');

  { Сначала просим новую версию Thermiqra
    завершиться штатно. }
  if FileExists(AppExe) then
  begin
    Exec(
      AppExe,
      '--shutdown',
      '',
      SW_HIDE,
      ewNoWait,
      ResultCode);

    Sleep(1500);
  end;

  { Для старых версий, которые ещё не понимают
    --shutdown, оставляем резервное закрытие. }
  if IsThermiqraRunning then
  begin
    Exec(
      ExpandConstant('{sys}\taskkill.exe'),
      '/F /T /IM "{#MyAppExeName}"',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);

    Sleep(500);
  end;
end;


function PrepareToInstall(
  var NeedsRestart: Boolean): String;
begin
  Result := '';

  CloseThermiqra;

  if IsThermiqraRunning then
  begin
    Result :=
      'Не удалось закрыть Thermiqra.' + #13#10 +
      'Закройте программу вручную и повторите установку.';
  end;
end;


function InitializeUninstall: Boolean;
begin
  CloseThermiqra;

  Result :=
    not IsThermiqraRunning;

  if not Result then
  begin
    MsgBox(
      'Не удалось закрыть Thermiqra.' + #13#10 + #13#10 +
      'Закройте программу вручную и снова запустите удаление.',
      mbError,
      MB_OK);
  end;
end;


procedure CurUninstallStepChanged(
  CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveUserData := False;

    if not UninstallSilent then
    begin
      RemoveUserData :=
        MsgBox(
          'Удалить также настройки и историю Thermiqra?' + #13#10 + #13#10 +
          'Да — удалить все пользовательские данные Thermiqra.' + #13#10 +
          'Нет — сохранить настройки и историю для повторной установки или обновления.',
          mbConfirmation,
          MB_YESNO
        ) = IDYES;
    end;
  end
  else if
    (CurUninstallStep = usPostUninstall) and
    RemoveUserData then
  begin
    DelTree(
      ExpandConstant(
        '{localappdata}\Thermiqra'),
      True,
      True,
      True);

    DelTree(
      ExpandConstant(
        '{localappdata}\PCHardwareMonitor'),
      True,
      True,
      True);
  end;
end;