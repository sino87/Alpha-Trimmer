# Development guide

[日本語](DEVELOPMENT.ja.md) · [README](../README.md) · [Localization](../localization/README.md)

The app uses C# and WPF. The shell extension uses C++. Run commands from the repository root.

Build and test on Windows x64. The distributed app targets Windows 11 x64.

## Requirements

| Tool | Requirement |
| --- | --- |
| .NET SDK | Version specified in `global.json` |
| .NET Desktop Runtime | .NET 8 x64, required for GUI tests |
| Visual Studio 2022 | C++ x64 build tools |
| Windows SDK | C++ headers and libraries |
| Inno Setup | 6.3 or later |
| PowerShell | Save scripts containing Japanese as UTF-8 with BOM |

Verified with .NET SDK 9.0.310, Inno Setup 6.6.1, and Windows PowerShell 5.1.

## Build

```powershell
.\scripts\Build.ps1
```

This generates localization files, runs the release, localization, installer, Core, and UI tests, and builds the app, shell DLL, and installer. The .NET runtime is bundled.

After building, run `.\scripts\Test-Shell.ps1` to test the shell extension. GitHub Actions runs both commands.

| Output | Location |
| --- | --- |
| Installer | `Installer/output/Alpha_Trimmer_Setup.exe` |
| App | `artifacts/app/` |

## GitHub Actions

Pushes and pull requests trigger tests and builds automatically.

To prepare a release, open **Prepare release** in Actions, select **Run workflow**, and choose `main`. A successful run attaches the installer, SHA-256 checksums, and a provenance attestation to a draft release. Review the draft before publishing.

Update only `<Version>` in `Directory.Build.props` to change the version. The app, installer, and Windows manifest use this value automatically. Add release notes to `CHANGELOG.md` and `CHANGELOG.ja.md`. A draft can be updated from the same commit. The workflow stops if the version is already published or its tag points to a different commit.

Verify the installer's provenance with GitHub CLI:

```powershell
gh attestation verify Alpha_Trimmer_Setup.exe --repo sino87/Alpha-Trimmer
```

## Run

```powershell
& .\artifacts\app\alpha_trimmer.exe
& .\artifacts\app\alpha_trimmer.exe 'C:\Images\sample.png' 'C:\Images\sample.webp'
```

Run without arguments to open the GUI. Pass image paths to process them immediately.

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
| UI | Actual WPF windows and rendered screenshots |
| Installer | Inno Setup migration conditions; does not install the app |
| Localization | Adding languages, fallback, and validation of translated strings |
| Shell | Passing multiple images from the DLL to the published EXE; excludes registry registration and Explorer display |
| Release | Versions, release notes, and rules for updating tags and releases |

Run `Build.ps1` before `Test-Shell.ps1`; the shell test uses the DLL and EXE in `artifacts/app/`.

Tests do not require Python. To regenerate image fixtures, use Pillow and `tests/generate_fixtures.py`.

Check dependencies for known vulnerabilities:

```powershell
dotnet list src/AlphaTrimmer.App/AlphaTrimmer.App.csproj package --vulnerable --include-transitive
```

## References

[Shell extension registration](https://learn.microsoft.com/en-us/windows/win32/shell/reg-shell-exts) · [Multiple selection](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-employ-the-verb-selection-model) · [Installation scope](https://jrsoftware.org/ishelp/topic_setup_privilegesrequiredoverridesallowed.htm)
