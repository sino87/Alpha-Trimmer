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
  SaveStringToFile(ExpandConstant('{param:result}'), 'PASS: 8 migration policies', False);
  Result := False;
end;
