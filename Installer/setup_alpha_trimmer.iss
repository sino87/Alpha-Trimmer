#ifndef AppSource
  #define AppSource "..\artifacts\app"
#endif
#ifndef AppVersion
  #error AppVersion must be supplied by scripts/Build.ps1
#endif

#define ShellFileName "AlphaTrimmer.Shell-" + GetSHA256OfFile(AppSource + "\AlphaTrimmer.Shell.dll") + ".dll"

[Setup]
AppId=Alpha Trimmer
AppName=Alpha Trimmer
DefaultDirName={autopf}\Alpha Trimmer
DefaultGroupName=Alpha Trimmer
AppVersion={#AppVersion}
AppPublisher=Tatsuya
AppPublisherURL=https://github.com/sino87/Alpha-Trimmer
AppSupportURL=https://github.com/sino87/Alpha-Trimmer/issues
OutputDir=output
OutputBaseFilename=Alpha_Trimmer_Setup-v{#AppVersion}
Compression=lzma2
SolidCompression=yes
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\alpha_trimmer.exe
MinVersion=10.0.22000
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UsePreviousPrivileges=yes
CloseApplications=yes
CloseApplicationsFilterExcludes=AlphaTrimmer.Shell*.dll
RestartApplications=no
WizardStyle=modern
ShowLanguageDialog=yes
LanguageDetectionMethod=uilanguage
Uninstallable=yes

#include "..\artifacts\localization\InstallerLanguages.iss"

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Excludes: "\AlphaTrimmer.Shell.dll"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#AppSource}\AlphaTrimmer.Shell.dll"; DestDir: "{app}"; DestName: "{#ShellFileName}"; Flags: onlyifdoesntexist uninsrestartdelete
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Alpha Trimmer"; Filename: "{app}\alpha_trimmer.exe"

[Registry]
Root: HKA; Subkey: "Software\Classes\CLSID\{{045D2ABC-85B9-4841-AB1E-FA6CE07E264D}"; ValueType: string; ValueName: ""; ValueData: "Alpha Trimmer"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\CLSID\{{045D2ABC-85B9-4841-AB1E-FA6CE07E264D}\InprocServer32"; ValueType: string; ValueName: ""; ValueData: "{app}\{#ShellFileName}"
Root: HKA; Subkey: "Software\Classes\CLSID\{{045D2ABC-85B9-4841-AB1E-FA6CE07E264D}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Apartment"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.png\shell\AlphaTrimmer\command"; Flags: deletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.png\shell\AlphaTrimmer"; ValueType: string; ValueName: ""; ValueData: "{cm:ContextMenuTitle}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.png\shell\AlphaTrimmer"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\icon.ico"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.png\shell\AlphaTrimmer"; ValueType: string; ValueName: "ExplorerCommandHandler"; ValueData: "{{045D2ABC-85B9-4841-AB1E-FA6CE07E264D}"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.png\shell\AlphaTrimmer"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\AlphaTrimmer\command"; Flags: deletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\AlphaTrimmer"; ValueType: string; ValueName: ""; ValueData: "{cm:ContextMenuTitle}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\AlphaTrimmer"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\icon.ico"""
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\AlphaTrimmer"; ValueType: string; ValueName: "ExplorerCommandHandler"; ValueData: "{{045D2ABC-85B9-4841-AB1E-FA6CE07E264D}"
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.webp\shell\AlphaTrimmer"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"

[Code]
#include "MigrationPolicy.iss"
#include "ShellUpdatePolicy.iss"
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Alpha Trimmer_is1';
  ShellKey = 'Software\Classes\CLSID\{045D2ABC-85B9-4841-AB1E-FA6CE07E264D}\InprocServer32';
var
  ShellRegistrationChanged: Boolean;

function PreviousShellNeedsRestart(RootKey: Integer): Boolean;
var
  PreviousPath: String;
begin
  Result := RegQueryStringValue(RootKey, ShellKey, '', PreviousPath) and
    ShouldRestartShell(PreviousPath, ExpandConstant('{app}\{#ShellFileName}'));
end;

function NeedRestart: Boolean;
begin
  Result := ShellRegistrationChanged;
end;

function RemovePreviousInstall(RootKey: Integer; var ErrorText: String): Boolean;
var
  Version, Uninstaller: String;
  ExitCode: Integer;
  Started: Boolean;
begin
  Result := True;
  if not RegQueryStringValue(RootKey, UninstallKey, 'DisplayVersion', Version) then Exit;
  if not ShouldRemoveInstall(Version, (RootKey = HKLM32) or (RootKey = HKLM64), IsAdminInstallMode) then Exit;
  if not RegQueryStringValue(RootKey, UninstallKey, 'UninstallString', Uninstaller) then
  begin
    ErrorText := CustomMessage('MissingUninstaller');
    Result := False;
    Exit;
  end;
  Uninstaller := RemoveQuotes(Uninstaller);
  if ((RootKey = HKLM32) or (RootKey = HKLM64)) and not IsAdminInstallMode then
    Started := ShellExec('runas', Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ExitCode)
  else
    Started := Exec(Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if not Started or (ExitCode <> 0) then
  begin
    ErrorText := CustomMessage('UninstallFailed');
    Result := False;
    Exit;
  end;
  if RegKeyExists(RootKey, UninstallKey) then
  begin
    ErrorText := CustomMessage('UninstallIncomplete');
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  ShellRegistrationChanged := ShellRegistrationChanged or
    PreviousShellNeedsRestart(HKCU) or PreviousShellNeedsRestart(HKLM64);
  if not RemovePreviousInstall(HKLM32, Result) then Exit;
  if not RemovePreviousInstall(HKLM64, Result) then Exit;
  if not RemovePreviousInstall(HKCU, Result) then Exit;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Files: TFindRec;
  Path: String;
begin
  if CurStep <> ssPostInstall then Exit;
  if FindFirst(ExpandConstant('{app}\AlphaTrimmer.Shell*.dll'), Files) then
  begin
    try
      repeat
        if CompareText(Files.Name, '{#ShellFileName}') <> 0 then
        begin
          Path := ExpandConstant('{app}\') + Files.Name;
          if not DeleteFile(Path) then Log('Keeping in-use shell extension: ' + Path);
        end;
      until not FindNext(Files);
    finally
      FindClose(Files);
    end;
  end;
end;
