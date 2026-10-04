$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testDirectory = Join-Path $projectRoot ('artifacts/installer-policy-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
& $iscc "/O$testDirectory" (Join-Path $projectRoot 'tests/InstallerPolicy.iss')
if ($LASTEXITCODE -ne 0) { throw 'インストーラーの移行ポリシーのコンパイルに失敗しました。' }
$resultPath = Join-Path $testDirectory 'result.txt'
$testExecutable = Join-Path $testDirectory 'InstallerPolicyTest.exe'
$process = Start-Process -FilePath $testExecutable -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', ('/result="' + $resultPath + '"')) -WindowStyle Hidden -Wait -PassThru
if (!(Test-Path -LiteralPath $resultPath)) { throw "移行ポリシーの検証に失敗しました: $($process.ExitCode)" }
$result = Get-Content -LiteralPath $resultPath -Raw
if ($result -ne 'PASS: 8 migration policies, 5 shell update policies') { throw '移行ポリシーの検証結果が不正です。' }
Write-Output $result

$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
$resolved = [IO.Path]::GetFullPath($testDirectory)
if (!$resolved.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'テストデータの保存先が不正です。' }
Remove-Item -LiteralPath $resolved -Recurse -Force
