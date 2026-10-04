param([switch]$CompileNative)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $projectRoot ('artifacts/localization-tests-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $testRoot 'source'
$output = Join-Path $testRoot 'output'
New-Item -ItemType Directory -Force -Path $source | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'localization/en.json') -Destination $source
$french = @{
    culture = 'fr'
    installerName = 'french'
    installerMessagesFile = 'compiler:Languages\French.isl'
    strings = @{ 'App.Close' = 'Fermer'; 'Shell.ContextMenuTitle' = 'Rogner l''image "alpha" <test> \' }
}
$frenchPath = Join-Path $source 'fr.json'
$french | ConvertTo-Json | Set-Content -LiteralPath $frenchPath -Encoding UTF8
& (Join-Path $PSScriptRoot 'Generate-Localization.ps1') -SourceDirectory $source -OutputDirectory $output
$header = Get-Content -LiteralPath (Join-Path $output 'ShellStrings.h') -Raw -Encoding UTF8
$installer = Get-Content -LiteralPath (Join-Path $output 'InstallerLanguages.iss') -Raw -Encoding UTF8
if (!$header.Contains('L"fr", L"Rogner l''image \"alpha\" <test> \\"') -or !$installer.Contains('Name: "french"')) {
    throw '言語ファイルの追加がメニュー・インストーラーに反映されません。'
}
if (!$installer.Contains('french.UninstallFailed=Uninstallation failed.%nUninstall Alpha Trimmer manually, then run the installer again.')) {
    throw 'インストーラーの未翻訳項目が英語に戻りません。'
}
Write-Output 'PASS: 翻訳ファイルの追加、メニューとインストーラーへの反映、未翻訳項目の英語表示'
if ($CompileNative) {
    $harness = Join-Path $output 'LocalizationSmoke.exe'
    & cl.exe /nologo /std:c++17 /EHsc /W4 /WX /O2 /MT /utf-8 (Join-Path $projectRoot 'tests/LocalizationSmoke.cpp') "/I$output" "/Fo$output/LocalizationSmoke.obj" "/Fe$harness"
    if ($LASTEXITCODE -ne 0) { throw '翻訳のC++生成結果をコンパイルできません。' }
    & $harness
    if ($LASTEXITCODE -ne 0) { throw '翻訳のC++生成結果で文字が変わっています。' }
}

function Assert-Rejected([string]$ExpectedMessage) {
    $rejected = $false
    try {
        & (Join-Path $PSScriptRoot 'Generate-Localization.ps1') -SourceDirectory $source -OutputDirectory $output
    } catch {
        if (!$_.Exception.Message.Contains($ExpectedMessage)) { throw }
        $rejected = $true
    }
    if (!$rejected) { throw "翻訳の不正を検出できません: $ExpectedMessage" }
    Write-Output "PASS: $ExpectedMessage"
}

$french.strings['Installer.MissingUninstaller'] = "Uninstaller missing.`nRemove manually."
$french | ConvertTo-Json | Set-Content -LiteralPath $frenchPath -Encoding UTF8
& (Join-Path $PSScriptRoot 'Generate-Localization.ps1') -SourceDirectory $source -OutputDirectory $output
$multiline = Get-Content -LiteralPath (Join-Path $output 'InstallerLanguages.iss') -Raw -Encoding UTF8
if (!$multiline.Contains('french.MissingUninstaller=Uninstaller missing.%nRemove manually.')) {
    throw 'インストーラーの改行を変換できません。'
}
Write-Output 'PASS: インストーラーの改行をInno Setup形式へ変換'
$originalTitle = $french.strings['Shell.ContextMenuTitle']
$french.strings['Shell.ContextMenuTitle'] = "First`nSecond"
$french | ConvertTo-Json | Set-Content -LiteralPath $frenchPath -Encoding UTF8
Assert-Rejected '使用できない文字'
$french.strings['Shell.ContextMenuTitle'] = $originalTitle
$french.strings['Installer.MissingUninstaller'] = "First`tSecond"
$french | ConvertTo-Json | Set-Content -LiteralPath $frenchPath -Encoding UTF8
Assert-Rejected '使用できない文字'
$french.strings.Remove('Installer.MissingUninstaller')

$french.strings['App.Unknown'] = 'Unknown'
$french | ConvertTo-Json | Set-Content -LiteralPath $frenchPath -Encoding UTF8
Assert-Rejected '英語に存在しない翻訳キー'
$french.strings.Remove('App.Unknown')
$french.strings['App.Close'] = ''
$french | ConvertTo-Json | Set-Content -LiteralPath $frenchPath -Encoding UTF8
Assert-Rejected '翻訳キーまたは文言が不正'
$french.strings['App.Close'] = 'Fermer'
$french.strings['Installer.UninstallFailed'] = '{invalid}'
$french | ConvertTo-Json | Set-Content -LiteralPath $frenchPath -Encoding UTF8
Assert-Rejected '使用できない文字'

$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
$resolved = [IO.Path]::GetFullPath($testRoot)
if (!$resolved.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'テストデータの保存先が不正です。' }
Remove-Item -LiteralPath $resolved -Recurse -Force
