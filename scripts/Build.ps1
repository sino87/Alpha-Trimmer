param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    . (Join-Path $PSScriptRoot 'ReleaseFunctions.ps1')
    $appVersion = Get-AppVersion $projectRoot
    & (Join-Path $PSScriptRoot 'Generate-Localization.ps1')
    if (!$SkipTests) {
        & (Join-Path $PSScriptRoot 'Test-Release.ps1')
        & (Join-Path $PSScriptRoot 'Test-Localization.ps1')
        & (Join-Path $PSScriptRoot 'Test-Installer.ps1')
        & dotnet run --project tests/AlphaTrimmer.Tests -c Release
        if ($LASTEXITCODE -ne 0) { throw '画像処理のテストに失敗しました。' }
        & dotnet run --project tests/AlphaTrimmer.UiTests -c Release -- artifacts/gui-tests
        if ($LASTEXITCODE -ne 0) { throw 'GUIの動作テストに失敗しました。' }
    }
    $outputPath = Join-Path $projectRoot 'artifacts/app'
    $artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath($outputPath)
    if (!$resolved.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'ビルド出力の保存先が不正です。' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    & dotnet publish src/AlphaTrimmer.App -c Release -r win-x64 --self-contained true -o $outputPath -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw 'C#アプリのビルドに失敗しました。' }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (!$visualStudio) { throw 'Visual C++ x64ビルドツールが必要です。' }
    Import-Module (Join-Path $visualStudio 'Common7/Tools/Microsoft.VisualStudio.DevShell.dll')
    Enter-VsDevShell -VsInstallPath $visualStudio -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64'
    & cl.exe /nologo /std:c++17 /EHsc /W4 /WX /O2 /MT /utf-8 /DUNICODE /D_UNICODE /LD src/AlphaTrimmer.Shell/ShellExtension.cpp "/Fo$projectRoot/artifacts/ShellExtension.obj" /link /WX /DEF:src/AlphaTrimmer.Shell/ShellExtension.def "/IMPLIB:$projectRoot/artifacts/AlphaTrimmer.Shell.lib" "/OUT:$outputPath/AlphaTrimmer.Shell.dll" ole32.lib shell32.lib shlwapi.lib uuid.lib
    if ($LASTEXITCODE -ne 0) { throw '右クリック拡張のビルドに失敗しました。' }
    Copy-Item -LiteralPath Installer/icon.ico -Destination $outputPath
    Copy-Item -LiteralPath licenses -Destination $outputPath -Recurse -Force
    $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    $compileArguments = @("/DAppSource=$outputPath", "/DAppVersion=$appVersion")
    & $iscc @compileArguments Installer/setup_alpha_trimmer.iss
    if ($LASTEXITCODE -ne 0) { throw 'インストーラーのビルドに失敗しました。' }
} finally {
    Pop-Location
}
