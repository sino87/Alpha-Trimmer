function Get-AppVersion([string]$ProjectRoot) {
    [xml]$properties = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Directory.Build.props') -Raw
    $versions = @($properties.Project.PropertyGroup.Version | Where-Object { $_ })
    if ($versions.Count -ne 1 -or $versions[0] -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw 'アプリのバージョン定義が不正です。'
    }
    return [string]$versions[0]
}

function Get-ReleaseMetadata([string]$ProjectRoot) {
    $version = Get-AppVersion $ProjectRoot
    $tag = 'v' + $version
    $escaped = [regex]::Escape($tag)
    $notes = @{}
    foreach ($language in @('en', 'ja')) {
        $file = if ($language -eq 'en') { 'CHANGELOG.md' } else { 'CHANGELOG.ja.md' }
        $path = Join-Path $ProjectRoot $file
        if (!(Test-Path -LiteralPath $path)) { throw '該当バージョンの変更履歴が見つからないか重複しています。' }
        $changelog = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        $sections = [regex]::Matches($changelog, '(?ms)^## ' + $escaped + '\r?\n(.*?)(?=^## |\z)')
        if ($sections.Count -ne 1 -or [string]::IsNullOrWhiteSpace($sections[0].Groups[1].Value)) {
            throw '該当バージョンの変更履歴が見つからないか重複しています。'
        }
        $notes[$language] = $sections[0].Groups[1].Value.Trim()
    }
    [pscustomobject]@{ Version = $version; Tag = $tag; Notes = $notes.en + "`n`n## 日本語`n`n" + $notes.ja + "`n" }
}

function Invoke-ReleaseApi([string]$Repository, [string]$Path, [string]$Method = 'GET', $Body = $null, [switch]$AllowMissing) {
    if (!$env:GH_TOKEN) { throw 'GitHubの認証トークンがありません。' }
    $request = @{
        Uri = "https://api.github.com/repos/$Repository/$Path"
        Method = $Method
        Headers = @{ Authorization = "Bearer $env:GH_TOKEN"; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
    }
    if ($null -ne $Body) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10 -Compress))
    }
    try { Invoke-RestMethod @request }
    catch {
        if ($AllowMissing -and $_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { return $null }
        throw
    }
}

function Get-RepositoryReleaseState([string]$Repository, [string]$Tag, [string]$Commit, [switch]$IncludeDrafts) {
    if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or $Commit -notmatch '^[0-9a-f]{40}$' -or $Tag -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+$') {
        throw 'リポジトリ、タグ、コミットの指定が不正です。'
    }
    $release = Invoke-ReleaseApi $Repository "releases/tags/$Tag" -AllowMissing
    if ($IncludeDrafts -and $null -eq $release) {
        for ($page = 1; ; $page++) {
            $releases = @(Invoke-ReleaseApi $Repository "releases?per_page=100&page=$page")
            $matching = @($releases | Where-Object { $_.tag_name -ceq $Tag })
            if ($matching.Count -gt 1) { throw '同じタグのリリースが重複しています。' }
            if ($matching.Count -eq 1) {
                $release = $matching[0]
                break
            }
            if ($releases.Count -lt 100) { break }
        }
    }
    if ($null -ne $release -and !$release.draft) { throw 'このバージョンは公開済みです。更新を中止しました。' }
    $reference = Invoke-ReleaseApi $Repository "git/ref/tags/$Tag" -AllowMissing
    if ($null -ne $reference) {
        $target = $reference.object
        for ($depth = 0; $target.type -eq 'tag' -and $depth -lt 8; $depth++) {
            $target = (Invoke-ReleaseApi $Repository ("git/tags/" + $target.sha)).object
        }
        if ($target.type -ne 'commit' -or $target.sha -cne $Commit) { throw '既存タグが別のコミットを指しています。タグは変更しません。' }
    }
    [pscustomobject]@{ Release = $release; TagExists = $null -ne $reference }
}
