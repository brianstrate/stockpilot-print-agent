#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "StockPilot Print Agent"
#define MyAppPublisher "Simpelt NET / JB Strate"
#define MyAppURL "https://simpeltnet.dk"
#define MyGuiExeName "StockPilotPrintConfig.exe"
#define MyServiceExeName "StockPilotPrintService.exe"
#define MyServiceName "StockPilotPrintAgent"

[Setup]
AppId={{8D6E4A2B-3F19-4C7A-9B1E-5D2A7C9F1042}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\StockPilotPrintAgent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=StockPilotPrintAgent-Setup-v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
CloseApplications=force
CloseApplicationsFilter=*.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
Name: "{commonappdata}\StockPilotPrintAgent"; Permissions: users-modify

[Files]
Source: "publish\service\*"; DestDir: "{app}\service"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\gui\*"; DestDir: "{app}\gui"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName} (Indstillinger)"; Filename: "{app}\gui\{#MyGuiExeName}"
Name: "{group}\Afinstaller {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\gui\{#MyGuiExeName}"; Tasks: desktopicon

[Run]
; Stop og slet evt. eksisterende version af Windows-servicen før genoprettelse
Filename: "sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden

; Opret Windows Service med automatisk opstart ved boot (kører uden bruger-login)
Filename: "sc.exe"; Parameters: "create {#MyServiceName} binPath= ""\""{app}\service\{#MyServiceExeName}\"" --contentRoot \""{app}\service\"""" start= auto DisplayName= ""StockPilot Print Agent Service"""; Flags: runhidden; StatusMsg: "Installerer StockPilot Print Service..."
Filename: "sc.exe"; Parameters: "description {#MyServiceName} ""Lydløs udskrift af kvitteringer og labels fra WordPress / WooCommerce i baggrunden."""; Flags: runhidden

; Automatisk genstart ved nedbrud/netværksudfald (efter 5s, 10s og 30s)
Filename: "sc.exe"; Parameters: "failure {#MyServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000"; Flags: runhidden

; Start servicen med det samme
Filename: "sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden; StatusMsg: "Starter StockPilot Print Service..."

; Åbn WPF GUI-konfiguratoren efter installation (springes over ved /SILENT auto-update)
Filename: "{app}\gui\{#MyGuiExeName}"; Description: "Åbn {#MyAppName} indstillinger"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopPrintService"
Filename: "sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeletePrintService"