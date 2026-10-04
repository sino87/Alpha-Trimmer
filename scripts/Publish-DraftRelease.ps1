param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Commit,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'ReleaseFunctions.ps1')
$metadata = Get-ReleaseMetadata $projectRoot
$state = Get-RepositoryReleaseState $Repository $metadata.Tag $Commit
if ($CheckOnly) {
    if ($env:GITHUB_OUTPUT) {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value @("version=$($metadata.Version)", "tag=$($metadata.Tag)") -Encoding UTF8
    }
    Write-Output "リリース条件を確認しました: $($metadata.Tag)"
    return
}
$releaseDirectory = Join-Path $projectRoot 'artifacts/release'
$assets = @('Alpha_Trimmer_Setup.exe', 'SHA256SUMS.txt', 'provenance.sigstore.json') | ForEach-Object { Join-Path $releaseDirectory $_ }
foreach ($asset in $assets) {
    if (!(Test-Path -LiteralPath $asset -PathType Leaf)) { throw "配布ファイルがありません: $asset" }
}
$expectedChecksum = (Get-FileHash -LiteralPath $assets[0] -Algorithm SHA256).Hash.ToLowerInvariant() + '  Alpha_Trimmer_Setup.exe'
if ((Get-Content -LiteralPath $assets[1] -Raw).Trim() -cne $expectedChecksum) { throw 'インストーラーとSHA-256が一致しません。' }
if (!$state.TagExists) {
    $null = Invoke-ReleaseApi $Repository 'git/refs' 'POST' @{ ref = "refs/tags/$($metadata.Tag)"; sha = $Commit }
}
$body = @{ name = $metadata.Tag; body = $metadata.Notes }
if ($null -eq $state.Release) {
    $body.tag_name = $metadata.Tag
    $body.target_commitish = $Commit
    $body.draft = $true
    $body.prerelease = $false
    $release = Invoke-ReleaseApi $Repository 'releases' 'POST' $body
}
else {
    $release = Invoke-ReleaseApi $Repository ("releases/" + $state.Release.id) 'PATCH' $body
}
$null = Get-RepositoryReleaseState $Repository $metadata.Tag $Commit
& gh release upload $metadata.Tag @assets --repo $Repository --clobber
if ($LASTEXITCODE -ne 0) { throw '下書きへのファイル添付に失敗しました。' }
Write-Output "リリース下書きを更新しました: $($release.html_url)"
if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "[リリース下書き]($($release.html_url))を確認して公開してください。" -Encoding UTF8
}
