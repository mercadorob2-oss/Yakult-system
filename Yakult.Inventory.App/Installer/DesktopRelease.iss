; Used by Build-DesktopRelease.ps1. Inno Setup remains the packaging engine.
#ifndef ReleaseVersion
  #define ReleaseVersion "1.6.0.0"
#endif
#ifndef BuildOutput
  #define BuildOutput "..\bin\Release"
#endif
#ifndef ReleaseOutput
  #define ReleaseOutput "Output"
#endif
#ifndef InstallerBaseName
  #define InstallerBaseName "YISP_Setup_" + ReleaseVersion
#endif
#ifndef ReleaseDatabaseSettings
  #define ReleaseDatabaseSettings AddBackslash(BuildOutput) + "appsettings.json"
#endif

[Setup]
AppId={{98EE8B2E-10D8-4524-A66B-FE50D449AEA4}
AppName=Yakult Inventory Monitoring System
AppVersion={#ReleaseVersion}
AppPublisher=Yakult
DefaultDirName={localappdata}\Yakult Inventory Monitoring System
UsePreviousAppDir=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#ReleaseOutput}
OutputBaseFilename={#InstallerBaseName}
UninstallDisplayIcon={app}\Yakult.Inventory.App.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
DisableProgramGroupPage=yes
#ifdef ReleaseSignTool
SignTool={#ReleaseSignTool}
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#BuildOutput}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.vshost.*,appsettings*.json"; Flags: ignoreversion recursesubdirs createallsubdirs
; Only the selected base connection is packaged. Existing database settings are retained.
Source: "{#ReleaseDatabaseSettings}"; DestDir: "{app}"; DestName: "appsettings.json"; Flags: onlyifdoesntexist

[Icons]
Name: "{autoprograms}\Yakult Inventory Monitoring System"; Filename: "{app}\Yakult.Inventory.App.exe"
Name: "{autodesktop}\Yakult Inventory Monitoring System"; Filename: "{app}\Yakult.Inventory.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Yakult.Inventory.App.exe"; Description: "Launch Yakult"; Flags: nowait postinstall skipifsilent

[Code]
var PreviousConfiguration: String;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ConfigurationPath: String;
begin
  Result := '';
  PreviousConfiguration := '';
  ConfigurationPath := ExpandConstant('{app}\Yakult.Inventory.App.exe.config');
  if FileExists(ConfigurationPath) then
  begin
    PreviousConfiguration := ConfigurationPath + '.before-update';
    if FileExists(PreviousConfiguration) then
      Result := 'A previous Yakult settings backup needs recovery. Ask IT to restore the .before-update configuration before retrying.'
    else if not FileCopy(ConfigurationPath, PreviousConfiguration, False) then
      Result := 'Could not back up your existing Yakult settings. Close Yakult and try again.';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var ResultCode: Integer;
begin
  if (CurStep = ssPostInstall) and (PreviousConfiguration <> '') then
  begin
    if not Exec(ExpandConstant('{app}\Yakult.DesktopUpdater.exe'),
      '--merge-config "' + PreviousConfiguration + '" "' + ExpandConstant('{app}\Yakult.Inventory.App.exe.config') + '"',
      ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('Could not restore your Yakult settings. Setup could not start the settings helper.');
    if ResultCode <> 0 then
      RaiseException('Could not restore your Yakult settings. Setup has not completed successfully.');
    DeleteFile(PreviousConfiguration);
  end;
end;

function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    MsgBox('Yakult requires Microsoft .NET Framework 4.8. Ask IT to install it, then run this setup again.', mbError, MB_OK);
end;
