$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
Import-Module (Join-Path $visualStudio 'Common7/Tools/Microsoft.VisualStudio.DevShell.dll')
Enter-VsDevShell -VsInstallPath $visualStudio -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64'
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'Generate-Localization.ps1')
    & (Join-Path $PSScriptRoot 'Test-Localization.ps1') -CompileNative
    $artifacts = Join-Path $projectRoot 'artifacts'
    $harness = Join-Path $artifacts 'ShellSmoke.exe'
    & cl.exe /nologo /std:c++17 /EHsc /W4 /WX /O2 /MT /utf-8 /DUNICODE /D_UNICODE tests/ShellSmoke.cpp "/Fo$artifacts/ShellSmoke.obj" "/Fe$harness" /link /WX ole32.lib shell32.lib uuid.lib
    if ($LASTEXITCODE -ne 0) { throw 'シェル検証プログラムのビルドに失敗しました。' }
    $testDirectory = Join-Path $artifacts ('shell-smoke 日本語 ' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $testDirectory | Out-Null
    $png = Join-Path $testDirectory '画像 sample.png'
    $webp = Join-Path $testDirectory '画像 sample.webp'
    Copy-Item -LiteralPath tests/AlphaTrimmer.Tests/Fixtures/rgba.png -Destination $png
    Copy-Item -LiteralPath tests/AlphaTrimmer.Tests/Fixtures/rgba.webp -Destination $webp
    $pngHash = (Get-FileHash -LiteralPath $png).Hash
    $webpHash = (Get-FileHash -LiteralPath $webp).Hash
    & $harness (Join-Path $artifacts 'app/AlphaTrimmer.Shell.dll') $png $webp
    if ($LASTEXITCODE -ne 0) { throw "右クリック拡張の呼び出しに失敗しました: $LASTEXITCODE" }
    $pngOutput = Join-Path $testDirectory '画像 sample-Trimmed-1.png'
    $webpOutput = Join-Path $testDirectory '画像 sample-Trimmed-1.webp'
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while (!(Test-Path -LiteralPath $webpOutput) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 100
    }
    if (!(Test-Path -LiteralPath $pngOutput) -or !(Test-Path -LiteralPath $webpOutput)) { throw '複数選択の出力が揃いません。' }
    if ((Get-FileHash -LiteralPath $png).Hash -ne $pngHash -or (Get-FileHash -LiteralPath $webp).Hash -ne $webpHash) { throw '元画像が変更されています。' }
    $artifactsRoot = [IO.Path]::GetFullPath($artifacts) + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath($testDirectory)
    if (!$resolved.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'テストデータの保存先が不正です。' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    Write-Output 'PASS: 実際のCOM拡張 → 公開用EXE → 日本語・空白を含むPNG/WebPの複数保存、元画像保持'
} finally {
    Pop-Location
}
