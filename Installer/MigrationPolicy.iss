function ShouldRemoveInstall(Version: String; ExistingAllUsers, TargetAllUsers: Boolean): Boolean;
begin
  Result := (Pos('1.', Version) = 1) or (ExistingAllUsers <> TargetAllUsers);
end;
