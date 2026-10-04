# Localization

[日本語](README.ja.md) · [README](../README.md) · [Development guide](../docs/DEVELOPMENT.md)

`en.json` defines the English baseline. Japanese translations are in `ja.json`.

## Edit text

Edit `strings` in the relevant JSON file, then rebuild from the repository root:

```powershell
.\scripts\Build.ps1
```

| Prefix | Used in |
| --- | --- |
| `App.` | Startup messages, result dialogs, and buttons |
| `Gui.` | GUI |
| `Result.` | Reasons for skipped or failed images |
| `Error.` | Custom image processing errors |
| `Shell.` | Context menu |
| `Installer.` | Custom installer messages |

`Shell.ContextMenuTitle` defines the menu label and is also used by the installer. GUI help text uses line breaks to separate items and backticks to display text as inline code.

## Add a language

1. Copy `en.json` to a file named for the language, such as `fr.json` for French.
2. Set `culture` to `fr`, `installerName` to `french`, and `installerMessagesFile` to `compiler:Languages\\French.isl`.
3. Translate `strings`. Remove untranslated keys rather than leaving empty strings.
4. Rebuild and check the UI on Windows configured for that language.

The filename must match `culture`, which must be a culture recognized by .NET. Standard installer messages require the specified `.isl` file. For languages not included in Inno Setup, add the `.isl` file and update the build configuration. Escape backslashes as `\\` in JSON.

Missing translations fall back from the regional culture to its parent language, then to English. For example: `fr-CA` → `fr` → `en`. The app, context menu, and custom installer messages use the same order.

## Keys and allowed characters

Add new keys to `en.json` before referencing them in code. Other languages can be translated later.

| Scope | Restrictions |
| --- | --- |
| All strings | Keys absent from English, empty strings, and invalid cultures cause build errors |
| `Shell.` | No line breaks, control characters, `{`, `}`, or `%` |
| `Installer.` | `\n` line breaks are allowed; other control characters, `{`, `}`, and `%` are not |

Installer line breaks are converted to `%n` during generation. Error details from Windows and the image processing library are displayed as provided.

Translations are embedded in the app. C++ headers and Inno Setup language definitions are generated in `artifacts/localization/`. Rebuild and reinstall to apply changes. Do not edit generated files.

## Verify

```powershell
.\scripts\Test-Localization.ps1
dotnet run --project tests/AlphaTrimmer.Tests -c Release
.\scripts\Test-Shell.ps1
```

These tests cover adding languages, fallback, invalid text, and menu language switching. `Test-Shell.ps1` generates C++ from translations containing symbols and compiles it. Save PowerShell scripts containing Japanese as UTF-8 with BOM.
