# Alpha Trimmer

[English](#english) | [日本語](#japanese)

<a name="english"></a>
## English

A Windows tool that automatically trims excess transparent areas from PNG and WebP images via the context menu.

### Features

- Simply right-click an image file and select "Trim Transparency" to use.
- The original image is preserved, and the trimmed version is automatically saved with a new name (e.g., `image-1.png`).
- Multiple images can be selected and processed at once.
- Supports both PNG and WebP formats.

### Installation

1. Download the installer `Alpha_Trimmer_Setup.exe` from [Releases](https://github.com/YOUR_USERNAME/AlphaTrimmer/releases).
2. Run the installer and follow the on-screen instructions.

### Usage

1. Select the PNG or WebP image(s) you want to trim.
2. Right-click to open the context menu.
3. Click "Trim Transparency".
4. The trimmed image(s) will be saved in the same folder.

### For Developers

#### Requirements

- Windows 10
- Python 3.x

#### Build Instructions

Install required libraries
```bash
pip install Pillow PyInstaller
```

Build executable
Run `build_alpha_trimmer.bat`

Create Installer
Compile `Installer/setup_alpha_trimmer.iss` using Inno Setup.

---

<a name="japanese"></a>
## 日本語

Windowsの右クリックメニューから、PNGおよびWebP画像の余分な透明部分を自動的にトリミングするツールです。

### 機能

- 画像ファイルを右クリックして「透明部分をトリミング」を選ぶだけで使えます。
- 元の画像は上書きされず、自動的に別名（例: `image-1.png`）で保存されます。
- 複数の画像を選択して一度に処理することも可能です。
- PNGとWebPの両方の形式に対応しています。

### インストール方法

1. [Releases](https://github.com/YOUR_USERNAME/AlphaTrimmer/releases) からインストーラー `Alpha_Trimmer_Setup.exe` をダウンロードします。
2. インストーラーを実行し、画面の指示に従ってインストールしてください。

### 使い方

1. トリミングしたいPNGまたはWebP画像を選択します。
2. 右クリックしてメニューを開きます。
3. 「透明部分をトリミング」をクリックします。
4. 同じフォルダにトリミングされた画像が保存されます。

### 開発者向け情報

#### 動作環境

- Windows 10
- Python 3.x

#### ビルド方法

必要なライブラリのインストール
```bash
pip install Pillow PyInstaller
```

exe化
`build_alpha_trimmer.bat` を実行

インストーラー作成
Inno Setup で `Installer/setup_alpha_trimmer.iss` をコンパイル
