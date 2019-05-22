#define Configuration GetEnv('CONFIGURATION')
#if Configuration == ""
#define Configuration "Release"
#endif

#define Version GetEnv('BUILD_VERSION')
#if Version == ""
#define Version "x.x.x.x"
#endif

[Setup]
AppName=Intiface Game Haptics Router
AppVersion={#Version}
AppPublisher=Nonpolynomial Labs, LLC
AppPublisherURL=www.buttplug.io
AppId={{4c1ec14a-7c27-11e9-af02-ff7da5ea73cd}
SetupIconFile=icons\intiface-ghr-logo-1.ico
WizardImageFile=icons\intiface-ghr-logo-1.bmp
WizardSmallImageFile=icons\intiface-ghr-logo-1.bmp
DefaultDirName={pf}\IntifaceGameHapticsRouter
UninstallDisplayIcon=icons\intiface-ghr-logo-1.ico
Compression=lzma2
SolidCompression=yes
OutputBaseFilename=intiface-game-haptics-router-installer
OutputDir=.\installer
LicenseFile=LICENSE

[Dirs]
Name: "{localappdata}\IntifaceGameHapticsRouter"

[Files]
Source: "IntifaceGameHapticsRouter\bin\{#Configuration}\*.exe"; DestDir: "{app}"
Source: "IntifaceCLI\bin\{#Configuration}\*.dll"; DestDir: "{app}"
Source: "IntifaceCLI\bin\{#Configuration}\*.config"; DestDir: "{app}"
Source: "Readme.md"; DestDir: "{app}"; DestName: "Readme.txt"
Source: "LICENSE"; DestDir: "{app}"; DestName: "License.txt"

// [Run]
// Filename: "{app}\Readme.txt"; Description: "View the README file"; Flags: postinstall shellexec unchecked

[Code]

// Uninstall on install code taken from https://stackoverflow.com/a/2099805/4040754
////////////////////////////////////////////////////////////////////
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;


/////////////////////////////////////////////////////////////////////
function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;


/////////////////////////////////////////////////////////////////////
function UnInstallOldVersion(): Integer;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
// Return Values:
// 1 - uninstall string is empty
// 2 - error executing the UnInstallString
// 3 - successfully executed the UnInstallString

  // default return value
  Result := 0;

  // get the uninstall string of the old app
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    if Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES','', SW_HIDE, ewWaitUntilTerminated, iResultCode) then
      Result := 3
    else
      Result := 2;
  end else
    Result := 1;
end;

/////////////////////////////////////////////////////////////////////
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep=ssInstall) then
  begin
    if (IsUpgrade()) then
    begin
      UnInstallOldVersion();
    end;
  end;
  if (CurStep=ssPostInstall) then
  begin
    WriteEnginePath();
  end;
end;
