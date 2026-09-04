#define MyAppName "tc_format"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0.0"
#endif

#ifndef MyAppVersionInfo
  #define MyAppVersionInfo "1.0.0.0"
#endif

#ifndef MyPublishDir
  #define MyPublishDir "..\artifacts\publish\win-x64"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "..\artifacts\installer"
#endif

#ifndef MyVsixPath
  #define MyVsixPath "..\src\TcFormat.Xae\bin\Release\net472\TcFormat.Xae.vsix"
#endif

#ifndef MyVsixContentDir
  #define MyVsixContentDir "..\artifacts\extension\TcFormat.Xae"
#endif

#define XaeExtensionDirectory "TcFormat.Xae"

[Setup]
AppId={{8D00E37C-13E2-4485-B690-F671B782B23B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=tc_format contributors
AppReadmeFile={app}\README.md
VersionInfoVersion={#MyAppVersionInfo}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
UsePreviousSetupType=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesEnvironment=yes
CloseApplications=yes
CloseApplicationsFilter=tc_format.exe
Compression=lzma2
SolidCompression=yes
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-win-x64-setup
UninstallDisplayIcon={app}\tc_format.ico
SetupIconFile=..\assets\tc_format.ico
WizardStyle=modern dynamic

[Types]
Name: "full"; Description: "CLI and TwinCAT XAE integration"
Name: "compact"; Description: "CLI only"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "cli"; Description: "tc_format command-line formatter"; Types: full compact custom; Flags: fixed
Name: "xae"; Description: "TwinCAT XAE editor integration"; Types: full; Check: IsXaeAvailable

[Files]
Source: "{#MyPublishDir}\tc_format.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\tc_format.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\examples\.editorconfig"; DestDir: "{app}\examples"; Flags: ignoreversion
Source: "{#MyVsixPath}"; DestDir: "{app}\integration"; DestName: "TcFormat.Xae.vsix"; Components: xae; Flags: ignoreversion
Source: "{#MyVsixContentDir}\*"; DestDir: "{code:GetXaeExtensionDirectory}"; Components: xae; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: filesandordirs; Name: "{code:GetXaeExtensionDirectory}"; Components: xae

[UninstallDelete]
Type: filesandordirs; Name: "{code:GetXaeExtensionDirectory}"

[Registry]
Root: HKLM64; Subkey: "Software\tc_format"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}\tc_format.exe"; Flags: uninsdeletekey

[Code]
const
  SystemEnvironmentKey =
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  PathValueName = 'Path';

var
  XaeExtensionWasInstalled: Boolean;

function GetXaeInstallDirectory(Param: String): String;
begin
  if RegQueryStringValue(
    HKLM64,
    'Software\Beckhoff\TcXaeShell\17.0',
    'InstallDir',
    Result) and
    FileExists(Result + 'Common7\IDE\TcXaeShell.exe') then
  begin
    Exit;
  end;

  Result := ExpandConstant('{%ProgramW6432}\Beckhoff\TcXaeShell');
  if not FileExists(Result + '\Common7\IDE\TcXaeShell.exe') then
  begin
    Result := ExpandConstant('{pf32}\Beckhoff\TcXaeShell');
  end;
end;

function GetXaeExtensionDirectory(Param: String): String;
begin
  Result := GetXaeInstallDirectory('') +
    '\Common7\IDE\Extensions\{#XaeExtensionDirectory}';
end;

function IsXaeAvailable: Boolean;
begin
  Result := FileExists(GetXaeInstallDirectory('') + '\Common7\IDE\TcXaeShell.exe');
end;

function IsXaeRunning: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(
    ExpandConstant('{cmd}'),
    '/D /C tasklist.exe /FI "IMAGENAME eq TcXaeShell.exe" /NH | ' +
      'find.exe /I "TcXaeShell.exe" >NUL',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpReady) and WizardIsComponentSelected('xae') and
    IsXaeRunning then
  begin
    MsgBox(
      'TwinCAT XAE is running. Save your work and close every XAE window. ' +
        'Then choose Install again to continue.',
      mbError,
      MB_OK);
    Result := False;
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  XaeExtensionWasInstalled := DirExists(GetXaeExtensionDirectory(''));
  if XaeExtensionWasInstalled and IsXaeRunning then
  begin
    MsgBox(
      'TwinCAT XAE is running. Save your work and close every XAE window ' +
        'before uninstalling tc_format.',
      mbError,
      MB_OK);
    Result := False;
  end;
end;

procedure RefreshXaeConfiguration(FailOnError: Boolean);
var
  ResultCode: Integer;
  XaeExecutable: String;
begin
  XaeExecutable := GetXaeInstallDirectory('') + '\Common7\IDE\TcXaeShell.exe';
  if not FileExists(XaeExecutable) then
  begin
    Exit;
  end;

  if (not Exec(
      XaeExecutable,
      '/setup',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode)) or (ResultCode <> 0) then
  begin
    if FailOnError then
    begin
      RaiseException(
        'TwinCAT XAE could not rebuild its extension cache. Setup cannot ' +
          'complete the XAE integration.');
    end;
  end;
end;

function NormalizePathEntry(Value: String): String;
begin
  Result := Trim(Value);
  if (Length(Result) >= 2) and (Result[1] = '"') and
     (Result[Length(Result)] = '"') then
  begin
    Result := Copy(Result, 2, Length(Result) - 2);
  end;

  while (Length(Result) > 3) and (Result[Length(Result)] = '\') do
  begin
    Delete(Result, Length(Result), 1);
  end;

  Result := Lowercase(Result);
end;

function PathContainsEntry(PathValue: String; Entry: String): Boolean;
var
  Candidate: String;
  Remaining: String;
  Separator: Integer;
begin
  Result := False;
  Remaining := PathValue;

  repeat
    Separator := Pos(';', Remaining);
    if Separator = 0 then
    begin
      Candidate := Remaining;
      Remaining := '';
    end
    else
    begin
      Candidate := Copy(Remaining, 1, Separator - 1);
      Delete(Remaining, 1, Separator);
    end;

    if NormalizePathEntry(Candidate) = NormalizePathEntry(Entry) then
    begin
      Result := True;
      Exit;
    end;
  until Remaining = '';
end;

function PathWithoutEntry(PathValue: String; Entry: String): String;
var
  Candidate: String;
  Remaining: String;
  Separator: Integer;
begin
  Result := '';
  Remaining := PathValue;

  repeat
    Separator := Pos(';', Remaining);
    if Separator = 0 then
    begin
      Candidate := Remaining;
      Remaining := '';
    end
    else
    begin
      Candidate := Copy(Remaining, 1, Separator - 1);
      Delete(Remaining, 1, Separator);
    end;

    if (Candidate <> '') and
       (NormalizePathEntry(Candidate) <> NormalizePathEntry(Entry)) then
    begin
      if Result <> '' then
      begin
        Result := Result + ';';
      end;
      Result := Result + Candidate;
    end;
  until Remaining = '';
end;

procedure AddInstallDirectoryToPath;
var
  InstallDirectory: String;
  PathValue: String;
begin
  InstallDirectory := ExpandConstant('{app}');
  if not RegQueryStringValue(HKLM64, SystemEnvironmentKey, PathValueName, PathValue) then
  begin
    PathValue := '';
  end;

  if PathContainsEntry(PathValue, InstallDirectory) then
  begin
    Exit;
  end;

  if (PathValue <> '') and (PathValue[Length(PathValue)] <> ';') then
  begin
    PathValue := PathValue + ';';
  end;

  if not RegWriteExpandStringValue(
    HKLM64,
    SystemEnvironmentKey,
    PathValueName,
    PathValue + InstallDirectory) then
  begin
    RaiseException('Unable to add tc_format to the system PATH.');
  end;
end;

procedure RemoveInstallDirectoryFromPath;
var
  InstallDirectory: String;
  PathValue: String;
  UpdatedPath: String;
begin
  InstallDirectory := ExpandConstant('{app}');
  if not RegQueryStringValue(HKLM64, SystemEnvironmentKey, PathValueName, PathValue) then
  begin
    Exit;
  end;

  if not PathContainsEntry(PathValue, InstallDirectory) then
  begin
    Exit;
  end;

  UpdatedPath := PathWithoutEntry(PathValue, InstallDirectory);
  if UpdatedPath = '' then
  begin
    RegDeleteValue(HKLM64, SystemEnvironmentKey, PathValueName);
  end
  else
  begin
    RegWriteExpandStringValue(HKLM64, SystemEnvironmentKey, PathValueName, UpdatedPath);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    AddInstallDirectoryToPath;
    if WizardIsComponentSelected('xae') then
    begin
      RefreshXaeConfiguration(True);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveInstallDirectoryFromPath;
    if XaeExtensionWasInstalled then
    begin
      RefreshXaeConfiguration(False);
    end;
  end;
end;
