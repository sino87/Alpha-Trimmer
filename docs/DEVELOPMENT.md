# Development guide

[日本語](DEVELOPMENT.ja.md) · [README](../README.md) · [Localization](../localization/README.md)

The app uses C# and WPF; the context menu extension uses C++. Build and test on Windows x64. The distributed app supports Windows 11 x64.

---

Run the following commands from the repository root.

## Requirements

| Tool | Requirement |
| --- | --- |
| .NET SDK | Version specified in `global.json` |
| .NET Desktop Runtime | 8 x64, for GUI tests |
| Visual Studio 2022 | C++ x64 build tools |
| Windows SDK | C++ headers and libraries |
| Inno Setup | 6.3 or later |
| PowerShell | Tested with Windows PowerShell 5.1 |

Save PowerShell scripts containing Japanese as UTF-8 with BOM.

---

## Build

```powershell
.\scripts\Build.ps1
.\scripts\Test-Shell.ps1
```

`Build.ps1` generates localization files, runs tests, then builds the app, shell DLL, and installer. The installer includes the .NET runtime.

`Test-Shell.ps1` tests image processing through the context menu extension. GitHub Actions runs both commands.

| Output | Location |
| --- | --- |
| Installer | `Installer/output/Alpha_Trimmer_Setup-v<version>.exe` |
| App | `artifacts/app/` |

Shell DLL filenames include their SHA-256 hash; identical files are not overwritten. Shell DLLs are excluded from automatic application closing. Changing the registered DLL path prompts for a Windows restart. Later installations attempt to remove old DLLs that are still in use.

---

## Version updates

Change `<Version>` in `Directory.Build.props`. The app, installer, and Windows manifest share this value.

Write changes in English in `CHANGELOG.md` and Japanese in `CHANGELOG.ja.md`.

---

## GitHub Actions

Pushes and pull requests trigger tests and builds. Changes limited to READMEs or `docs/` skip these checks. Changelogs and files bundled with the installer remain covered.

Releases are prepared manually.

1. In Actions, open **Prepare release** → **Run workflow**, select `main`, and run it.
2. Review and publish the draft release.

Each manual run performs all tests and builds, then attaches the installer, SHA-256 checksums, and a provenance attestation to the draft.

A draft can be updated from the same commit. The workflow stops if the version is already published or its tag points to a different commit.

Verify the installer's provenance with GitHub CLI. Use the filename of the downloaded installer.

```powershell
gh attestation verify Alpha_Trimmer_Setup-v2.0.0.exe --repo sino87/Alpha-Trimmer
```

---

## Run

```powershell
& .\artifacts\app\alpha_trimmer.exe
& .\artifacts\app\alpha_trimmer.exe 'C:\Images\sample.png' 'C:\Images\sample.webp'
```

Run without arguments to open the GUI. Pass image paths to process them immediately.

---

## Tests

```powershell
dotnet run --project tests/AlphaTrimmer.Tests -c Release
dotnet run --project tests/AlphaTrimmer.UiTests -c Release -- artifacts/gui-tests
.\scripts\Test-Installer.ps1
.\scripts\Test-Localization.ps1
.\scripts\Test-Shell.ps1
.\scripts\Test-Release.ps1
```

| Test | Coverage |
| --- | --- |
| Core | Image processing and settings |
| UI | WPF window behavior and rendering |
| Installer | Migration from earlier versions and restart conditions for DLL updates |
| Localization | Adding languages, fallback, and invalid strings |
| Shell | Passing multiple images from the DLL to the distributed EXE |
| Release | Versions, changelogs, and rules for updating tags and releases |

Shell tests require the built DLL and EXE. Run `Build.ps1` first.

Automated tests do not cover actual installation, registry registration, or menu display in Explorer.

Tests do not require Python. To regenerate image fixtures, use Pillow and `tests/generate_fixtures.py`.

---

Check dependencies for known vulnerabilities with this command.

```powershell
dotnet list src/AlphaTrimmer.App/AlphaTrimmer.App.csproj package --vulnerable --include-transitive
```

---

## References

[Shell extension registration](https://learn.microsoft.com/en-us/windows/win32/shell/reg-shell-exts) · [Multiple selection](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-employ-the-verb-selection-model) · [Installation scope](https://jrsoftware.org/ishelp/topic_setup_privilegesrequiredoverridesallowed.htm)
