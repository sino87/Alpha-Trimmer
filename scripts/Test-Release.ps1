$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'ReleaseFunctions.ps1')
$testRoot = Join-Path $projectRoot ('artifacts/release-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $testRoot | Out-Null
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
$changelog = "# Changelog`n`n## v2.0.0`n`n- English change`n`n### Limitations`n`n- Limitation`n`n## v1.0.0`n`n- Old English`n"
$japaneseChangelog = "# 変更履歴`n`n## v2.0.0`n`n- 日本語の変更`n`n### 制限事項`n`n- 制限`n`n## v1.0.0`n`n- 古い日本語`n"
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.md'), $changelog, $encoding)
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.ja.md'), $japaneseChangelog, $encoding)
$metadata = Get-ReleaseMetadata $testRoot
Assert ($metadata.Version -eq '2.0.0' -and $metadata.Tag -eq 'v2.0.0') 'コードからバージョンとタグを取得'
Assert ($metadata.Notes.Contains('日本語の変更') -and $metadata.Notes.Contains('English change') -and $metadata.Notes.Contains('Limitation') -and !$metadata.Notes.Contains('古い日本語') -and !$metadata.Notes.Contains('Old English')) '該当バージョンの日英と制限事項だけを抽出'
Assert ($metadata.Notes.StartsWith('- English change') -and $metadata.Notes.IndexOf('English change') -lt $metadata.Notes.IndexOf('日本語の変更')) '英語を先に表示'
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.ja.md'), $japaneseChangelog.Replace('## v2.0.0', '## v2.0.1'), $encoding)
Assert-Rejected { Get-ReleaseMetadata $testRoot } '変更履歴が見つからない'
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.ja.md'), $japaneseChangelog.Replace('## v1.0.0', '## v2.0.0'), $encoding)
Assert-Rejected { Get-ReleaseMetadata $testRoot } '重複しています'
Remove-Item -LiteralPath (Join-Path $testRoot 'CHANGELOG.ja.md')
Assert-Rejected { Get-ReleaseMetadata $testRoot } '変更履歴が見つからない'
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.ja.md'), $japaneseChangelog, $encoding)
[IO.File]::WriteAllText((Join-Path $testRoot 'Directory.Build.props'), '<Project><PropertyGroup><Version>2.0.1</Version></PropertyGroup></Project>', $encoding)
Assert ((Get-AppVersion $testRoot) -eq '2.0.1') '一箇所の変更でビルド用バージョンが変わる'
Assert-Rejected { Get-ReleaseMetadata $testRoot } '変更履歴が見つからない'
[IO.File]::WriteAllText((Join-Path $testRoot 'Directory.Build.props'), '<Project><PropertyGroup><Version>invalid</Version></PropertyGroup></Project>', $encoding)
Assert-Rejected { Get-AppVersion $testRoot } 'バージョン定義が不正'
[IO.File]::WriteAllText((Join-Path $testRoot 'Directory.Build.props'), '<Project><PropertyGroup><Version>2.0.0</Version></PropertyGroup></Project>', $encoding)
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.md'), $changelog.Replace('## v2.0.0', '## v2.0.1'), $encoding)
Assert-Rejected { Get-ReleaseMetadata $testRoot } '変更履歴が見つからない'
[IO.File]::WriteAllText((Join-Path $testRoot 'CHANGELOG.md'), $changelog.Replace('## v1.0.0', '## v2.0.0'), $encoding)
Assert-Rejected { Get-ReleaseMetadata $testRoot } '重複しています'

$script:fakeRelease = $null
$script:fakePages = @{ 1 = @() }
$script:apiPaths = [Collections.Generic.List[string]]::new()
$script:fakeReference = $null
$script:fakeTag = $null
function Invoke-ReleaseApi([string]$Repository, [string]$Path, [string]$Method = 'GET', $Body = $null, [switch]$AllowMissing) {
    if ($Method -ne 'GET') { throw '検査中に書き込みが発生しました。' }
    $script:apiPaths.Add($Path)
    if ($Path.StartsWith('releases/tags/')) {
        if ($null -ne $script:fakeRelease -and $script:fakeRelease.draft) { throw 'タグAPIで下書きを返すテストは不正です。' }
        return $script:fakeRelease
    }
    if ($Path -match '^releases\?per_page=100&page=([0-9]+)$') { return $script:fakePages[[int]$Matches[1]] }
    if ($Path.StartsWith('git/ref/tags/')) { return $script:fakeReference }
    if ($Path.StartsWith('git/tags/')) { return $script:fakeTag }
    throw "想定外のAPI呼び出し: $Path"
}
$commit = 'a' * 40
$state = Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit -IncludeDrafts
Assert (!$state.TagExists -and $null -eq $state.Release) '初回リリースは作成可能'
$script:fakePages[1] = @([pscustomobject]@{ id = 1; draft = $true; tag_name = 'v2.0.0' })
$script:fakeReference = [pscustomobject]@{ object = [pscustomobject]@{ type = 'commit'; sha = $commit } }
$state = Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit -IncludeDrafts
Assert ($state.TagExists -and $state.Release.draft) '同じコミットの下書きは更新可能'
$script:apiPaths.Clear()
$state = Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit
Assert ($null -eq $state.Release -and !($script:apiPaths | Where-Object { $_.StartsWith('releases?') })) '読み取り専用の事前確認では下書きを検索しない'
$script:fakePages[1] = @(1..100 | ForEach-Object { [pscustomobject]@{ id = $_; draft = $false; tag_name = "v1.0.$_" } })
$script:fakePages[2] = @([pscustomobject]@{ id = 101; draft = $true; tag_name = 'v2.0.0' })
$state = Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit -IncludeDrafts
Assert ($state.Release.id -eq 101) '2ページ目の下書きを取得'
$script:fakePages[2] = @()
$state = Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit -IncludeDrafts
Assert ($null -eq $state.Release) '全ページに対象がなければ新規作成可能'
$script:fakePages[1] = @([pscustomobject]@{ id = 1; draft = $false; tag_name = 'v2.0.0' })
Assert-Rejected { Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit -IncludeDrafts } '公開済み'
$script:fakePages[1] = @(1..2 | ForEach-Object { [pscustomobject]@{ id = $_; draft = $true; tag_name = 'v2.0.0' } })
Assert-Rejected { Get-RepositoryReleaseState 'owner/repo' 'v2.0.0' $commit -IncludeDrafts } '重複しています'
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
