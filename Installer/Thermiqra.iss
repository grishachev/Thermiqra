#define MyAppName "Thermiqra"
#define MyAppVersion "0.8.1"
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
OutputBaseFilename=Thermiqra_Setup_0.8.1

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

[CustomMessages]
english.DesktopIcon=Create a desktop icon
russian.DesktopIcon=Создать значок на рабочем столе
english.AdditionalIcons=Additional icons:
russian.AdditionalIcons=Дополнительные значки:
english.InstallPawnIO=Installing the PawnIO system driver...
russian.InstallPawnIO=Установка системного драйвера PawnIO...
english.RunThermiqra=Run Thermiqra
russian.RunThermiqra=Запустить Thermiqra
english.CloseInstallFailed=Thermiqra could not be closed.
russian.CloseInstallFailed=Не удалось закрыть Thermiqra.
english.CloseInstallRetry=Close the application manually and retry the installation.
russian.CloseInstallRetry=Закройте программу вручную и повторите установку.
english.CloseUninstallRetry=Close the application manually and run the uninstaller again.
russian.CloseUninstallRetry=Закройте программу вручную и снова запустите удаление.
english.RemoveUserDataQuestion=Also remove Thermiqra settings and history?
russian.RemoveUserDataQuestion=Удалить также настройки и историю Thermiqra?
english.RemoveUserDataYes=Yes — remove all Thermiqra user data.
russian.RemoveUserDataYes=Да — удалить все пользовательские данные Thermiqra.
english.RemoveUserDataNo=No — keep settings and history for reinstalling or updating.
russian.RemoveUserDataNo=Нет — сохранить настройки и историю для повторной установки или обновления.

[Tasks]

Name: "desktopicon"; \
    Description: "{cm:DesktopIcon}"; \
    GroupDescription: "{cm:AdditionalIcons}"; \
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
    StatusMsg: "{cm:InstallPawnIO}"; \
    Flags: waituntilterminated runhidden


; ============================================================
; ЗАПУСК THERMIQRA ПОСЛЕ УСТАНОВКИ
; ============================================================

Filename: "{app}\Thermiqra.exe"; \
    Description: "{cm:RunThermiqra}"; \
    Flags: nowait postinstall skipifsilent runascurrentuser

; При автоматическом обновлении установщик работает в silent/very silent.
; Эта отдельная строка запускает новую Thermiqra после успешного обновления.
Filename: "{app}\Thermiqra.exe"; \
    Flags: nowait skipifnotsilent


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
      ExpandConstant('{cm:CloseInstallFailed}') + #13#10 +
      ExpandConstant('{cm:CloseInstallRetry}');
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
      ExpandConstant('{cm:CloseInstallFailed}') + #13#10 + #13#10 +
      ExpandConstant('{cm:CloseUninstallRetry}'),
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
          ExpandConstant('{cm:RemoveUserDataQuestion}') + #13#10 + #13#10 +
          ExpandConstant('{cm:RemoveUserDataYes}') + #13#10 +
          ExpandConstant('{cm:RemoveUserDataNo}'),
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
