param(
    [string]$SourceDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'localization'),
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/localization')
)

$ErrorActionPreference = 'Stop'
$catalogs = @{}
$installerNames = @{}
foreach ($file in Get-ChildItem -LiteralPath $SourceDirectory -Filter '*.json' -File) {
    $catalog = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $culture = [Globalization.CultureInfo]::GetCultureInfo($catalog.culture)
    if (!$culture.Name -or $file.BaseName -cne $culture.Name -or $catalogs.ContainsKey($culture.Name)) {
        throw "言語名とファイル名が一致していないか重複しています: $($file.Name)"
    }
    if ($catalog.installerName -notmatch '^[a-z][a-z0-9_]*$' -or $installerNames.ContainsKey($catalog.installerName)) {
        throw "インストーラーの言語名が不正か重複しています: $($file.Name)"
    }
    if ($catalog.installerMessagesFile -notmatch '^compiler:(Default\.isl|Languages\\[A-Za-z0-9_-]+\.isl)$') {
        throw "Inno Setupの言語ファイル指定が不正です: $($file.Name)"
    }
    if (!$catalog.strings -or !$catalog.strings.PSObject.Properties) { throw "翻訳がありません: $($file.Name)" }
    foreach ($property in $catalog.strings.PSObject.Properties) {
        if ($property.Name -notmatch '^[A-Za-z]+\.[A-Za-z]+$' -or $property.Value -isnot [string] -or [string]::IsNullOrWhiteSpace($property.Value)) {
            throw "翻訳キーまたは文言が不正です: $($file.Name) / $($property.Name)"
        }
        if (($property.Name.StartsWith('Installer.') -and $property.Value -match '[\x00-\x09\x0b-\x1f{}%]') -or
            ($property.Name.StartsWith('Shell.') -and $property.Value -match '[\x00-\x1f{}%]')) {
            throw "メニュー・インストーラー文言に使用できない文字があります: $($file.Name) / $($property.Name)"
        }
    }
    $catalogs[$culture.Name] = $catalog
    $installerNames[$catalog.installerName] = $true
}
if (!$catalogs.ContainsKey('en')) { throw '基準言語のen.jsonがありません。' }
$keys = @($catalogs['en'].strings.PSObject.Properties.Name)
foreach ($catalog in $catalogs.Values) {
    foreach ($key in $catalog.strings.PSObject.Properties.Name) {
        if ($keys -cnotcontains $key) { throw "英語に存在しない翻訳キーです: $($catalog.culture) / $key" }
    }
}

function Resolve-Text([string]$CultureName, [string]$Key) {
    $candidate = [Globalization.CultureInfo]::GetCultureInfo($CultureName)
    while ($candidate.Name) {
        if ($catalogs.ContainsKey($candidate.Name)) {
            $property = $catalogs[$candidate.Name].strings.PSObject.Properties[$Key]
            if ($null -ne $property) { return [string]$property.Value }
        }
        $candidate = $candidate.Parent
    }
    return [string]$catalogs['en'].strings.PSObject.Properties[$Key].Value
}

function ConvertTo-CppLiteral([string]$Value) {
    return 'L"' + $Value.Replace('\', '\\').Replace('"', '\"') + '"'
}

$ordered = @('en') + @($catalogs.Keys | Where-Object { $_ -ne 'en' } | Sort-Object)
$installer = [Collections.Generic.List[string]]::new()
$installer.Add('[Languages]')
foreach ($language in $ordered) {
    $catalog = $catalogs[$language]
    $installer.Add('Name: "' + $catalog.installerName + '"; MessagesFile: "' + $catalog.installerMessagesFile + '"')
}
$installer.Add('')
$installer.Add('[CustomMessages]')
$shell = [Collections.Generic.List[string]]::new()
$shell.Add('static constexpr ShellTranslation ShellTranslations[] = {')
foreach ($language in $ordered) {
    $catalog = $catalogs[$language]
    $title = Resolve-Text $language 'Shell.ContextMenuTitle'
    $installer.Add($catalog.installerName + '.ContextMenuTitle=' + $title)
    foreach ($key in $keys | Where-Object { $_.StartsWith('Installer.') }) {
        $installer.Add($catalog.installerName + '.' + $key.Substring(10) + '=' + (Resolve-Text $language $key).Replace("`n", '%n'))
    }
    $shell.Add('    { ' + (ConvertTo-CppLiteral $language) + ', ' + (ConvertTo-CppLiteral $title) + ' },')
}
$shell.Add('};')
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$encoding = [Text.UTF8Encoding]::new($true)
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'InstallerLanguages.iss'), $installer, $encoding)
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'ShellStrings.h'), $shell, $encoding)
Write-Output "翻訳ファイルを検証し、$($ordered.Count)言語のメニュー・インストーラー文言を生成しました。"
