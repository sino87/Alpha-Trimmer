[Setup]
AppId=AlphaTrimmerPolicyTest
AppName=Alpha Trimmer policy test
AppVersion=1
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
OutputBaseFilename=InstallerPolicyTest

[Code]
#include "..\Installer\MigrationPolicy.iss"
#include "..\Installer\ShellUpdatePolicy.iss"

procedure Check(Version: String; ExistingAllUsers, TargetAllUsers, Expected: Boolean);
begin
  if ShouldRemoveInstall(Version, ExistingAllUsers, TargetAllUsers) <> Expected then
    RaiseException('Migration policy failed: ' + Version);
end;

function InitializeSetup(): Boolean;
begin
  Check('1.0.0', False, False, True);
  Check('1.1.0', True, True, True);
  Check('1.0.0', False, True, True);
  Check('1.1.0', True, False, True);
  Check('2.0.0', False, False, False);
  Check('2.0.0', True, True, False);
  Check('2.0.0', False, True, True);
  Check('2.0.0', True, False, True);
  if ShouldRestartShell('', 'C:\app\AlphaTrimmer.Shell-new.dll') then
    RaiseException('Fresh install must not require a shell restart');
  if ShouldRestartShell('C:\app\AlphaTrimmer.Shell-new.dll', 'c:\APP\alphatrimmer.shell-NEW.dll') then
    RaiseException('Same shell must not require a restart');
  if not ShouldRestartShell('C:\app\AlphaTrimmer.Shell.dll', 'C:\app\AlphaTrimmer.Shell-new.dll') then
    RaiseException('Legacy shell update must require a restart');
  if not ShouldRestartShell('C:\app\AlphaTrimmer.Shell-old.dll', 'C:\app\AlphaTrimmer.Shell-new.dll') then
    RaiseException('Changed shell must require a restart');
  if not ShouldRestartShell('C:\old\AlphaTrimmer.Shell-new.dll', 'C:\new\AlphaTrimmer.Shell-new.dll') then
    RaiseException('Changed install location must require a restart');
  SaveStringToFile(ExpandConstant('{param:result}'), 'PASS: 8 migration policies, 5 shell update policies', False);
  Result := False;
end;
