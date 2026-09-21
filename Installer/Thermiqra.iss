#define MyAppName "Thermiqra"
#define MyAppVersion "0.2.0"
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

; Установщик всегда получает права администратора
PrivilegesRequired=admin

OutputDir=Output
OutputBaseFilename=Thermiqra_Setup_0.2.0

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
;
; ВАЖНО:
; runascurrentuser оставляет повышенные права установщика.
; Иначе postinstall запускается без повышения.
; ============================================================

Filename: "{app}\Thermiqra.exe"; \
    Description: "Запустить Thermiqra"; \
    Flags: nowait postinstall skipifsilent runascurrentuser


[UninstallDelete]

Type: filesandordirs; \
    Name: "{app}"