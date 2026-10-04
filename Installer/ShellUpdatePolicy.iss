function ShouldRestartShell(PreviousPath, TargetPath: String): Boolean;
begin
  Result := (PreviousPath <> '') and (CompareText(PreviousPath, TargetPath) <> 0);
end;
