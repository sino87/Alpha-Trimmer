$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'ReleaseFunctions.ps1')
$testRoot = Join-Path $projectRoot ('artifacts/release-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force (Join-Path $testRoot 'Installer') | Out-Null
$encoding = [Text.UTF8Encoding]::new($false)
$passed = 0

function Assert([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $script:passed++
    Write-Output "PASS: $Message"
}

function Assert-Rejected([scriptblock]$Action, [string]$Expected) {
    $rejected = $false
    try { $null = & $Action }
    catch {
        if (!$_.Exception.Message.Contains($Expected)) { throw }
        $rejected = $true
    }
    Assert $rejected $Expected
}

[IO.File]::WriteAllText((Join-Path $testRoot 'Directory.Build.props'), '<Project><PropertyGroup><Version>2.0.0</Version></PropertyGroup></Project>', $encoding)
[IO.File]::WriteAllText((Join-Path $testRoot 'Installer/setup_alpha_trimmer.iss'), "AppVersion=2.0.0`n", $encoding)
$changelog = "# 変更履歴`n`n## v2.0.0`n`n- 日本語の変更`n`n### 制限事項`n`n- 制限`n`n## v1.0.0`n`n- 古い日本語`n`n## English`n`n### v2.0.0`n`n- English change`n`n#### Limitations`n`n- Limitation`n`n### v1.0.0`n`n- Old English`n"
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.md'), $changelog, $encoding)
$metadata = Get-ReleaseMetadata $testRoot
Assert ($metadata.Version -eq '2.0.0' -and $metadata.Tag -eq 'v2.0.0') 'コードからバージョンとタグを取得'
Assert ($metadata.Notes.Contains('日本語の変更') -and $metadata.Notes.Contains('English change') -and $metadata.Notes.Contains('Limitation') -and !$metadata.Notes.Contains('古い日本語') -and !$metadata.Notes.Contains('Old English')) '該当バージョンの日英と制限事項だけを抽出'
[IO.File]::WriteAllText((Join-Path $testRoot 'Installer/setup_alpha_trimmer.iss'), "AppVersion=2.0.1`n", $encoding)
Assert-Rejected { Get-ReleaseMetadata $testRoot } 'バージョンが一致しません'
[IO.File]::WriteAllText((Join-Path $testRoot 'Installer/setup_alpha_trimmer.iss'), "AppVersion=2.0.0`n", $encoding)
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.md'), $changelog.Replace('### v2.0.0', '### v2.0.1'), $encoding)
Assert-Rejected { Get-ReleaseMetadata $testRoot } '変更履歴が見つからない'
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.md'), $changelog.Replace('## v1.0.0', '## v2.0.0'), $encoding)
Assert-Rejected { Get-ReleaseMetadata $testRoot } '重複しています'

$script:fakeRelease = $null
$script:fakeReference = $null
$script:fakeTag = $null
function Invoke-ReleaseApi([string]$Repository, [string]$Path, [string]$Method = 'GET', $Body = $null, [switch]$AllowMissing) {
    if ($Method -ne 'GET') { throw '検査中に書き込みが発生しました。' }
    if ($Path.StartsWith('releases/tags/')) { return $script:fakeRelease }
    if ($Path.StartsWith('git/ref/tags/')) { return $script:fakeReference }
    if ($Path.StartsWith('git/tags/')) { return $script:fakeTag }
    throw "想定外のAPI呼び出し: $Path"
}
$commit = 'a' * 40
$state = Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit
Assert (!$state.TagExists -and $null -eq $state.Release) '初回リリースは作成可能'
$script:fakeRelease = [pscustomobject]@{ id = 1; draft = $true }
$script:fakeReference = [pscustomobject]@{ object = [pscustomobject]@{ type = 'commit'; sha = $commit } }
$state = Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit
Assert ($state.TagExists -and $state.Release.draft) '同じコミットの下書きは更新可能'
$script:fakeRelease = [pscustomobject]@{ id = 1; draft = $false }
Assert-Rejected { Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit } '公開済み'
$script:fakeRelease = $null
$script:fakeReference.object.sha = 'b' * 40
Assert-Rejected { Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit } '別のコミット'
$script:fakeReference = [pscustomobject]@{ object = [pscustomobject]@{ type = 'tag'; sha = 'b' * 40 } }
$script:fakeTag = [pscustomobject]@{ object = [pscustomobject]@{ type = 'commit'; sha = $commit } }
Assert ((Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit).TagExists) '注釈付きタグもコミットまで確認'
$script:fakeTag.object.sha = 'c' * 40
Assert-Rejected { Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit } '別のコミット'
Assert-Rejected { Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' 'main' } '指定が不正'

$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
$resolved = [IO.Path]::GetFullPath($testRoot)
if (!$resolved.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'テストデータの保存先が不正です。' }
Remove-Item -LiteralPath $resolved -Recurse -Force
Write-Output "PASS: $passed release policies"
