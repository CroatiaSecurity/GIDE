; GIDE — Gorstaks IDE (C# .NET 4.8)
; Requires InnoSetup 6.x

#define MyAppName "GIDE"
#define MyAppVersion "0.4.0"
#define MyAppPublisher "Gorstak"
#define MyAppURL "https://github.com/tandrlemandrle/GIDE"
#define MyAppExeName "GIDE.exe"

[Setup]
AppId={{8A3F2B71-C4D5-4E6F-A7B8-9C0D1E2F3A4B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\GIDE
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\releases\{#MyAppVersion}
OutputBaseFilename=GIDE-Setup-{#MyAppVersion}
SetupIconFile=..\GIDE.ico
UninstallDisplayIcon={app}\GIDE.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "addtopath"; Description: "Add GIDE to system PATH"; GroupDescription: "System:"

[Registry]
Root: HKCU; Subkey: "Software\Classes\Directory\shell\GIDE"; ValueType: string; ValueName: ""; ValueData: "Run GIDE here"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\GIDE"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\GIDE.ico,0"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Directory\shell\GIDE\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --dir ""%1"""; Flags: uninsdeletevalue

Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\GIDE"; ValueType: string; ValueName: ""; ValueData: "Run GIDE here"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\GIDE"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\GIDE.ico,0"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Directory\Background\shell\GIDE\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --dir ""%V"""; Flags: uninsdeletevalue

[Files]
Source: "..\dist\GIDE.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\GIDE.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\GIDE"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\GIDE.ico"
Name: "{group}\Uninstall GIDE"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GIDE"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\GIDE.ico"; Tasks: desktopicon

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  Path: string;
  AppDir: string;
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('addtopath') then
    begin
      AppDir := ExpandConstant('{app}');
      RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', Path);
      if Pos(AppDir, Path) = 0 then
      begin
        Path := Path + ';' + AppDir;
        RegWriteStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', Path);
      end;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Path: string;
  AppDir: string;
  P: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    AppDir := ExpandConstant('{app}');
    RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', Path);
    P := Pos(';' + AppDir, Path);
    if P > 0 then
    begin
      Delete(Path, P, Length(';' + AppDir));
      RegWriteStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', Path);
    end;
  end;
end;