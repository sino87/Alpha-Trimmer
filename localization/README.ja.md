# 翻訳の管理

[English](README.md) · [README](../README.ja.md) · [開発ガイド](../docs/DEVELOPMENT.ja.md)

英語の`en.json`を基準とし、日本語は`ja.json`で管理します。

## 文言の変更

対象JSONの`strings`を編集し、リポジトリのルートで再ビルドします。

```powershell
.\scripts\Build.ps1
```

| 接頭辞 | 表示場所 |
| --- | --- |
| `App.` | 起動時の案内・結果画面・ボタン |
| `Gui.` | GUI |
| `Result.` | スキップ・失敗の理由 |
| `Error.` | 独自の画像処理エラー |
| `Shell.` | 右クリックメニュー |
| `Installer.` | インストーラーの独自メッセージ |

メニュー名は`Shell.ContextMenuTitle`で定義し、インストーラーでも共有します。GUIの説明文は改行で項目を分け、バッククォートで囲んだ部分をインラインコード風に表示します。

## 言語の追加

1. `en.json`をコピーして言語名のファイルを作ります。フランス語なら`fr.json`です。
2. `culture`を`fr`、`installerName`を`french`、`installerMessagesFile`を`compiler:Languages\\French.isl`に変更します。
3. `strings`を翻訳します。未翻訳のキーは削除できますが、空文字は使えません。
4. 再ビルドし、その言語のWindows環境で表示を確認します。

ファイル名は`culture`と一致させ、.NETが認識する言語名を使います。インストーラーの標準文言には指定した`.isl`が必要です。Inno Setupにない言語は`.isl`とビルド設定を追加します。JSONのバックスラッシュは`\\`と書きます。

未翻訳の項目は地域固有 → 親言語 → 英語の順に探します。たとえば`fr-CA` → `fr` → `en`です。アプリ、メニュー、インストーラーの独自文言で同じ順序を使います。

## キーと使用できる文字

新しいキーは先に`en.json`へ追加し、コードから参照します。他の言語は後から翻訳できます。

| 対象 | 制限 |
| --- | --- |
| 全体 | 英語にないキー、空文言、不正な言語名はビルドエラー |
| `Shell.` | 改行・制御文字・`{`・`}`・`%`は使用不可 |
| `Installer.` | `\n`の改行は使用可能。その他の制御文字・`{`・`}`・`%`は使用不可 |

インストーラーの改行は生成時に`%n`へ変換します。Windowsや画像処理ライブラリのエラー詳細は、提供元の文言をそのまま表示します。

翻訳はアプリへ埋め込み、C++ヘッダーとInno Setupの言語定義は`artifacts/localization/`に生成します。反映には再ビルド・再インストールが必要です。生成ファイルは編集しません。

## 検証

```powershell
.\scripts\Test-Localization.ps1
dotnet run --project tests/AlphaTrimmer.Tests -c Release
.\scripts\Test-Shell.ps1
```

言語追加、フォールバック、不正文言の検出、メニューの切り替えを確認します。`Test-Shell.ps1`は記号を含む翻訳をC++へ生成して実コンパイルします。日本語を含むPowerShellスクリプトはUTF-8 BOM付きで保存します。
