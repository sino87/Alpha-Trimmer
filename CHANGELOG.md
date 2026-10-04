# Changelog

[日本語](CHANGELOG.ja.md) · [README](README.md)

## v2.0.0

- Rebuilt in C#

  - Bundled the .NET runtime for Windows 11 x64

- Added a GUI

  - File and folder selection / drag and drop / range selection
  - Output folders / folder hierarchy / subfolder search / filename filtering
  - Stop and resume / retry failed images

- Added classic Explorer context menu support

  - Process multiple PNG and WebP images together
  - On standard Windows 11, the command appears under **Show more options**

- Improved image output

  - Preserve originals and partially transparent edges and shadows
  - Support 16-bit PNG and lossless output in the original format
  - Preserve supported metadata and update EXIF/XMP dimensions
  - Save with sequential names starting at `-Trimmed-1` to avoid overwriting

- Added display and settings options

  - Japanese and English, with translations managed in language-specific JSON
  - Light and dark themes / follow the Windows theme
  - Automatic settings saving / window position and size persistence

- Rebuilt the installer

  - Choose current-user or all-user installation
  - Migrate from v1
  - Remove the previous installation when changing the installation scope
  - Update the shell extension without automatically closing Explorer

### Limitations

- Supports static PNG and WebP images only
- Animated images and ARM64 builds are unsupported
- WebP output may be larger than the original
- Unknown metadata may not be retained
