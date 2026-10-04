# 開発ガイド

[English](DEVELOPMENT.md) · [README](../README.ja.md) · [翻訳](../localization/README.ja.md)

アプリはC#・WPF、右クリックメニューの拡張はC++製です。ビルド・テストはWindows x64で行います。配布版はWindows 11 x64対応です。

---

以下のコマンドは、リポジトリのルートで実行します。

## 必要な環境

| ツール | 条件 |
| --- | --- |
| .NET SDK | `global.json`の指定バージョン |
| .NET Desktop Runtime | 8 x64。GUIテスト用 |
| Visual Studio 2022 | C++ x64ビルドツール |
| Windows SDK | C++ヘッダーとライブラリ |
| Inno Setup | 6.3以降 |
| PowerShell | 検証済みはWindows PowerShell 5.1 |

日本語を含むPowerShellスクリプトは、UTF-8 BOM付きで保存します。

---

## ビルド

```powershell
.\scripts\Build.ps1
.\scripts\Test-Shell.ps1
```

`Build.ps1`で翻訳ファイルを生成し、テスト後にアプリ・シェルDLL・インストーラーを作成します。インストーラーには.NETランタイムも同梱します。

`Test-Shell.ps1`で右クリックメニューからの画像処理をテストします。GitHub Actionsでもこの2つを実行します。

| 出力 | 保存先 |
| --- | --- |
| インストーラー | `Installer/output/Alpha_Trimmer_Setup-v<version>.exe` |
| アプリ | `artifacts/app/` |

シェルDLLのファイル名にはSHA-256を付け、同じ内容なら上書きを省略します。DLLは自動終了の対象から除外し、登録先が変わる場合はWindowsの再起動を案内します。使用中の旧DLLは、次回以降のインストールで削除を試みます。

---

## バージョン更新

`Directory.Build.props`の`<Version>`を変更します。アプリ・インストーラー・Windows用マニフェストに共通で使われます。

変更内容は`CHANGELOG.md`に英語、`CHANGELOG.ja.md`に日本語で記載します。

---

## GitHub Actions

push・PRでテストとビルドを実行します。READMEや`docs/`だけの変更は省略します。変更履歴やインストーラーの同梱ファイルはテスト対象です。

リリースは手動で行います。

1. Actionsで「Prepare release」→「Run workflow」を開き、`main`を選んで実行します。
2. リリース下書きの内容を確認して公開します。

手動実行では毎回すべてのテストとビルドを行い、インストーラー・SHA-256・出所の証明を下書きに添付します。

同じコミットの下書きは更新できます。公開済みのバージョンや、既存タグと異なるコミットでは停止します。

配布元の確認にはGitHub CLIを使います。ファイル名はダウンロードしたものに合わせてください。

```powershell
gh attestation verify Alpha_Trimmer_Setup-v2.0.0.exe --repo sino87/Alpha-Trimmer
```

---

## 起動

```powershell
& .\artifacts\app\alpha_trimmer.exe
& .\artifacts\app\alpha_trimmer.exe 'C:\画像\sample.png' 'C:\画像\sample.webp'
```

引数なしならGUIが開きます。画像パスを渡すと、その場で処理します。

---

## テスト

```powershell
dotnet run --project tests/AlphaTrimmer.Tests -c Release
dotnet run --project tests/AlphaTrimmer.UiTests -c Release -- artifacts/gui-tests
.\scripts\Test-Installer.ps1
.\scripts\Test-Localization.ps1
.\scripts\Test-Shell.ps1
.\scripts\Test-Release.ps1
```

| テスト | 対象 |
| --- | --- |
| Core | 画像処理・設定 |
| UI | WPF画面の動作・描画 |
| Installer | 旧版からの移行・DLL更新時の再起動条件 |
| Localization | 言語追加・未翻訳時の表示・不正な文言 |
| Shell | DLLから配布用EXEへの複数画像の受け渡し |
| Release | バージョン・変更履歴・タグ・リリースの更新条件 |

Shellテストにはビルド済みのDLLとEXEが必要です。先に`Build.ps1`を実行してください。

実際のインストール、レジストリ登録、Explorerのメニュー表示は自動テストの対象外です。

通常のテストにPythonは不要です。テスト画像を作り直す場合は、Pillowと`tests/generate_fixtures.py`を使います。

---

依存パッケージの脆弱性を確認するコマンドです。

```powershell
dotnet list src/AlphaTrimmer.App/AlphaTrimmer.App.csproj package --vulnerable --include-transitive
```
---

## 参考資料

[シェル拡張の登録](https://learn.microsoft.com/en-us/windows/win32/shell/reg-shell-exts) · [複数選択](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-employ-the-verb-selection-model) · [インストール範囲](https://jrsoftware.org/ishelp/topic_setup_privilegesrequiredoverridesallowed.htm)
