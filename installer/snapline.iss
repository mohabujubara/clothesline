; Snapline installer. Built with Inno Setup 6:
;   ISCC.exe installer\snapline.iss /DVersion=1.5.0 /DSource=dist\portable
; Packages the self-contained build, so nothing else needs installing.

#ifndef Version
  #define Version "0.0.0"
#endif
#ifndef Source
  #define Source "..\dist\portable"
#endif

[Setup]
AppId={{7C1E3B2A-5D4F-4A6B-9C8D-1E2F3A4B5C6D}
AppName=Snapline
AppVersion={#Version}
AppVerName=Snapline {#Version}
AppPublisher=Snapline
AppPublisherURL=https://github.com/mohabujubara/snapline
AppSupportURL=https://github.com/mohabujubara/snapline/issues
AppUpdatesURL=https://github.com/mohabujubara/snapline/releases
DefaultDirName={autopf}\Snapline
DefaultGroupName=Snapline
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=Snapline-{#Version}-Setup
SetupIconFile=..\src\Snapline\Assets\Snapline.ico
UninstallDisplayIcon={app}\Snapline.exe
UninstallDisplayName=Snapline
WizardStyle=modern
WizardSmallImageFile=wizard-small.bmp
WizardImageFile=wizard.bmp
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl";

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start Snapline when I sign in"; GroupDescription: "Startup:"

[Files]
Source: "{#Source}\Snapline.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\src\Snapline\Assets\Fonts\OFL.txt"; DestDir: "{app}"; DestName: "Manrope-OFL.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\Snapline"; Filename: "{app}\Snapline.exe"
Name: "{group}\Uninstall Snapline"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Snapline"; Filename: "{app}\Snapline.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Snapline"; ValueData: """{app}\Snapline.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\Snapline.exe"; Description: "{cm:LaunchProgram,Snapline}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/IM Snapline.exe /F"; Flags: runhidden; RunOnceId: "StopSnapline"

[Code]
// Stop a running copy before the files are replaced, so the install never asks for a restart.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM Snapline.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

// The app's own Settings can turn "Start with Windows" on after installation.
// Uninstalling removes that entry whether Setup or the app wrote it.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Snapline');
end;
