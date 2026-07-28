#ifndef AppVersion
  #error AppVersion must be supplied by eng/package-installers.ps1
#endif
#ifndef PayloadDir
  #error PayloadDir must be supplied by eng/package-installers.ps1
#endif
#ifndef RepoRoot
  #error RepoRoot must be supplied by eng/package-installers.ps1
#endif

#define AppIdValue "{{B996831B-989B-4D72-BDDB-B87B11E22C16}"

[Setup]
AppId={#AppIdValue}
AppName=AvaScope
AppVersion={#AppVersion}
AppPublisher=RolandUI
AppPublisherURL=https://github.com/RolandUI/AvaScope
AppSupportURL=https://github.com/RolandUI/AvaScope/issues
AppUpdatesURL=https://github.com/RolandUI/AvaScope/releases
DefaultDirName={localappdata}\AvaScope
DefaultGroupName=AvaScope
DisableProgramGroupPage=yes
LicenseFile={#RepoRoot}\LICENSE
OutputBaseFilename=AvaScopeSetup
SetupIconFile={#RepoRoot}\assets\brand\avascope.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern dynamic
WizardSmallImageFile={#RepoRoot}\assets\brand\avascope-icon.png
WizardSmallImageFileDynamicDark={#RepoRoot}\assets\brand\avascope-icon-dark.png
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
ChangesEnvironment=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=AvaScope
UninstallDisplayIcon={app}\AvaScope.ico
VersionInfoVersion={#AppVersion}
VersionInfoCompany=RolandUI
VersionInfoDescription=AvaScope Setup
VersionInfoProductName=AvaScope
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "addtopath"; Description: "Add AvaScope to the current user's PATH"; GroupDescription: "Command-line integration:"; Flags: checkedonce

[InstallDelete]
Type: filesandordirs; Name: "{app}\current"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}\current"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\assets\brand\avascope.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoRoot}\eng\installer\avascope.cmd"; DestDir: "{app}\bin"; Flags: ignoreversion
Source: "{#RepoRoot}\eng\installer\verify-avascope.cmd"; DestDir: "{app}\bin"; Flags: ignoreversion

[Run]
Filename: "{cmd}"; Parameters: "/D /C ""{app}\bin\verify-avascope.cmd"""; WorkingDir: "{app}\bin"; Description: "Verify the AvaScope installation"; Flags: postinstall skipifsilent nowait

[UninstallDelete]
Type: files; Name: "{app}\avascope.discovery.json"
Type: filesandordirs; Name: "{app}\bin"
Type: filesandordirs; Name: "{app}\current"
Type: filesandordirs; Name: "{app}\sessions"
Type: filesandordirs; Name: "{app}\preview-sessions"

[Code]
const
  EnvironmentKey = 'Environment';
  ProductKey = 'Software\AvaScope';
  PathManagedValue = 'PathManaged';

function JsonEscape(Value: String): String;
begin
  Result := Value;
  StringChangeEx(Result, '\', '\\', True);
  StringChangeEx(Result, '"', '\"', True);
end;

function NormalizePathEntry(Value: String): String;
begin
  Result := Lowercase(RemoveQuotes(Trim(Value)));
  while (Length(Result) > 3) and (Result[Length(Result)] = '\') do
    Delete(Result, Length(Result), 1);
end;

function PathContains(const PathValue, Entry: String): Boolean;
var
  Remaining: String;
  Separator: Integer;
  Part: String;
begin
  Result := False;
  Remaining := PathValue;
  while Remaining <> '' do
  begin
    Separator := Pos(';', Remaining);
    if Separator = 0 then
    begin
      Part := Remaining;
      Remaining := '';
    end
    else
    begin
      Part := Copy(Remaining, 1, Separator - 1);
      Delete(Remaining, 1, Separator);
    end;

    if NormalizePathEntry(Part) = NormalizePathEntry(Entry) then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function RemovePathEntry(const PathValue, Entry: String): String;
var
  Remaining: String;
  Separator: Integer;
  Part: String;
begin
  Result := '';
  Remaining := PathValue;
  while Remaining <> '' do
  begin
    Separator := Pos(';', Remaining);
    if Separator = 0 then
    begin
      Part := Remaining;
      Remaining := '';
    end
    else
    begin
      Part := Copy(Remaining, 1, Separator - 1);
      Delete(Remaining, 1, Separator);
    end;

    Part := Trim(Part);
    if (Part <> '') and (NormalizePathEntry(Part) <> NormalizePathEntry(Entry)) then
    begin
      if Result <> '' then
        Result := Result + ';';
      Result := Result + Part;
    end;
  end;
end;

procedure AddCommandToPath;
var
  CurrentPath: String;
  CommandDirectory: String;
begin
  CommandDirectory := ExpandConstant('{app}\bin');
  if not RegQueryStringValue(HKCU, EnvironmentKey, 'Path', CurrentPath) then
    CurrentPath := '';

  if not PathContains(CurrentPath, CommandDirectory) then
  begin
    if (CurrentPath <> '') and (CurrentPath[Length(CurrentPath)] <> ';') then
      CurrentPath := CurrentPath + ';';
    CurrentPath := CurrentPath + CommandDirectory;
    RegWriteExpandStringValue(HKCU, EnvironmentKey, 'Path', CurrentPath);
    RegWriteDWordValue(HKCU, ProductKey, PathManagedValue, 1);
  end;
end;

procedure RemoveCommandFromPath;
var
  CurrentPath: String;
  PathManaged: Cardinal;
begin
  if RegQueryDWordValue(HKCU, ProductKey, PathManagedValue, PathManaged) and
     (PathManaged = 1) and
     RegQueryStringValue(HKCU, EnvironmentKey, 'Path', CurrentPath) then
  begin
    RegWriteExpandStringValue(
      HKCU,
      EnvironmentKey,
      'Path',
      RemovePathEntry(CurrentPath, ExpandConstant('{app}\bin')));
  end;

  RegDeleteValue(HKCU, ProductKey, PathManagedValue);
  RegDeleteKeyIfEmpty(HKCU, ProductKey);
end;

procedure WriteDiscoveryManifest;
var
  Content: String;
begin
  Content :=
    '{' + #13#10 +
    '  "schemaVersion": 1,' + #13#10 +
    '  "product": "AvaScope",' + #13#10 +
    '  "serviceName": "avascope",' + #13#10 +
    '  "version": "{#AppVersion}",' + #13#10 +
    '  "installMode": "per-user",' + #13#10 +
    '  "installRoot": "' + JsonEscape(ExpandConstant('{app}')) + '",' + #13#10 +
    '  "commandPath": "' + JsonEscape(ExpandConstant('{app}\bin\avascope.cmd')) + '",' + #13#10 +
    '  "executablePath": "' + JsonEscape(ExpandConstant('{app}\current\avascope.exe')) + '",' + #13#10 +
    '  "uninstallPath": "' + JsonEscape(ExpandConstant('{uninstallexe}')) + '",' + #13#10 +
    '  "mcp": {' + #13#10 +
    '    "transport": "stdio",' + #13#10 +
    '    "serverName": "avascope",' + #13#10 +
    '    "commandPath": "' + JsonEscape(ExpandConstant('{app}\bin\avascope.cmd')) + '",' + #13#10 +
    '    "arguments": ["mcp"]' + #13#10 +
    '  }' + #13#10 +
    '}' + #13#10;

  if not SaveStringToFile(ExpandConstant('{app}\avascope.discovery.json'), Content, False) then
    RaiseException('Could not write AvaScope discovery metadata.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    WriteDiscoveryManifest;
    if WizardIsTaskSelected('addtopath') then
      AddCommandToPath;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveCommandFromPath;
end;
