# 開発ガイド

[README](../README.md) · [翻訳](../localization/README.md)

アプリはC#・WPF、シェル拡張はC++で実装しています。コマンドはリポジトリのルートで実行します。

## 必要な環境

| ツール | 条件 |
| --- | --- |
| .NET SDK | `global.json`で指定したバージョン |
| Visual Studio 2022 | C++ x64ビルドツール |
| Windows SDK | C++ヘッダーとライブラリ |
| Inno Setup | 6.3以降 |
| PowerShell | 日本語を含むスクリプトはUTF-8 BOM付きで保存 |

検証環境は.NET SDK 9.0.310、Inno Setup 6.6.1、Windows PowerShell 5.1です。

## ビルド

```powershell
.\scripts\Build.ps1
```

翻訳生成、全テスト、アプリ・シェルDLL、インストーラーを順にビルドします。.NETランタイムは同梱します。

| 出力 | 保存先 |
| --- | --- |
| インストーラー | `Installer/output/Alpha_Trimmer_Setup.exe` |
| アプリ | `artifacts/app/` |

## GitHub Actions

ブランチへの更新とPRで、テスト・ビルドを自動実行します。

リリースはActionsの「Prepare release」→「Run workflow」で、`main`を選んで実行します。成功するとインストーラー・SHA-256・出所の証明をリリース下書きに添付します。内容を確認してから公開してください。

バージョンは`Directory.Build.props`とインストーラーの`AppVersion`を一致させ、`CHANGELOG.md`に日英の変更内容を記載します。同じコミットなら下書きを更新できます。公開済みのバージョン、または既存タグと異なるコミットでは停止します。

配布ファイルの出所はGitHub CLIで確認できます。

```powershell
gh attestation verify Alpha_Trimmer_Setup.exe --repo sino87/Alpha-Trimmer
```

## 起動

```powershell
& .\artifacts\app\alpha_trimmer.exe
& .\artifacts\app\alpha_trimmer.exe 'C:\画像\sample.png' 'C:\画像\sample.webp'
```

引数なしでGUI、画像パスを渡すとその場で処理します。

## テスト

```powershell
dotnet run --project tests/AlphaTrimmer.Tests -c Release
dotnet run --project tests/AlphaTrimmer.UiTests -c Release -- artifacts/gui-tests
.\scripts\Test-Installer.ps1
.\scripts\Test-Shell.ps1
.\scripts\Test-Release.ps1
```

| テスト | 対象 |
| --- | --- |
| Core | 画像処理・設定 |
| UI | 実際のWPF画面と描画画像 |
| Installer | Inno Setupの移行条件。インストールは行わない |
| Shell | DLLから配布用EXEへの複数画像受け渡し。レジストリ登録・Explorer表示は対象外 |
| Release | バージョン・変更履歴の整合性と、タグ・リリースの更新条件 |

通常のテストにPythonは不要です。画像の再生成にはPillowと`tests/generate_fixtures.py`を使います。

依存パッケージの脆弱性照会は次のコマンドで行います。

```powershell
dotnet list src/AlphaTrimmer.App/AlphaTrimmer.App.csproj package --vulnerable --include-transitive
```

## 参考資料

[シェル拡張の登録](https://learn.microsoft.com/en-us/windows/win32/shell/reg-shell-exts) · [複数選択](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-employ-the-verb-selection-model) · [インストール範囲](https://jrsoftware.org/ishelp/topic_setup_privilegesrequiredoverridesallowed.htm)
