# Media Device Copier

[![CI](https://github.com/drittich/media-device-copier/actions/workflows/ci.yml/badge.svg)](https://github.com/drittich/media-device-copier/actions/workflows/ci.yml)
[![Lint](https://github.com/drittich/media-device-copier/actions/workflows/lint.yml/badge.svg)](https://github.com/drittich/media-device-copier/actions/workflows/lint.yml)
[![Latest release](https://img.shields.io/github/v/release/drittich/media-device-copier)](https://github.com/drittich/media-device-copier/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![Platform](https://img.shields.io/badge/platform-Windows-blue)

**Media Device Copier** is a Windows command-line utility for copying files to and from phones and other devices connected via MTP (Media Transfer Protocol).

Use it to:

- list connected devices
- list files on a device folder
- download from device to PC
- upload from PC to device

## Table of contents

- [Media Device Copier](#media-device-copier)
  - [Table of contents](#table-of-contents)
  - [Features](#features)
  - [Requirements](#requirements)
  - [Quick start](#quick-start)
    - [1) Show help](#1-show-help)
    - [2) List devices](#2-list-devices)
    - [3) List files in a device folder](#3-list-files-in-a-device-folder)
    - [4) Download from device to PC](#4-download-from-device-to-pc)
    - [5) Upload from PC to device](#5-upload-from-pc-to-device)
  - [Commands](#commands)
    - [list-devices](#list-devices)
    - [list-files](#list-files)
    - [download-files](#download-files)
    - [upload-files](#upload-files)
  - [Filtering](#filtering)
  - [Common behaviors and defaults](#common-behaviors-and-defaults)
  - [Troubleshooting](#troubleshooting)
    - [Device not found](#device-not-found)
    - [Folder not found](#folder-not-found)
    - [Invalid regex](#invalid-regex)
    - [Unsupported file types](#unsupported-file-types)
    - [Failed files and exit code](#failed-files-and-exit-code)
    - [Empty folders and transient device errors](#empty-folders-and-transient-device-errors)
  - [Architecture (resilient downloads)](#architecture-resilient-downloads)
  - [Building from source](#building-from-source)
  - [Continuous integration](#continuous-integration)
  - [Releasing](#releasing)
  - [License](#license)
  - [Contributing](#contributing)

---

## Features

- **List devices**: show all connected MTP devices (`list-devices`, alias `l`).
- **List files**: list files in a device folder (`list-files`, alias `lf`), optionally filtered by regex and/or printed as full device paths.
- **Upload files**: copy files/folders from your PC to the device (`upload-files`, alias `u`).
- **Download files**: copy files/folders from the device to your PC (`download-files`, alias `d`).
- **Skip existing files**: avoid re-copying files (default behavior).
- **Move files**: use `--move` to delete the source after a successful transfer.
- **Recursive copy**: copy directory trees (`--copy-recursive`).
- **Subfolder + file filtering**: use regex patterns to include only matching subfolders and/or files.

---

## Requirements

- Windows (the app targets `net10.0-windows`).
- An MTP-capable device connected and unlocked (phones often require you to unlock and approve the connection).

---

## Quick start

### 1) Show help

```powershell
MediaDeviceCopier.exe -h
```

### 2) List devices

```powershell
MediaDeviceCopier.exe list-devices
# alias
MediaDeviceCopier.exe l
```

### 3) List files in a device folder

```powershell
MediaDeviceCopier.exe list-files -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE"

# alias + filter to only jpeg files + print full device paths
MediaDeviceCopier.exe lf -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE" -f "\.(jpg|jpeg)$" --full-path
```

### 4) Download from device to PC

```powershell
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE" -t "D:\Photos" -r
```

### 5) Upload from PC to device

```powershell
MediaDeviceCopier.exe upload-files -n "Android Device" -s "C:\Documents" -t "Internal Storage\Documents" -r -f "\.pdf$"
```

---

## Commands

### list-devices

Lists all available MTP devices.

- **Command**: `list-devices`
- **Alias**: `l`

Example:

```powershell
MediaDeviceCopier.exe list-devices
```

---

### list-files

Lists files in a device folder.

- **Command**: `list-files`
- **Alias**: `lf`

Options:

- `-n`, `--device-name` (required): the MTP device name.
- `-s`, `--source-folder` (required): the device folder to list.
- `-f`, `--filter-files` (optional): .NET regex to filter **by file name**.
- `--full-path` (optional): print full device paths instead of just file names.

Usage:

```powershell
MediaDeviceCopier.exe list-files -n "<Device>" -s "<DeviceFolder>" [-f "<regex>"] [--full-path]
```

Examples:

```powershell
# List all files in a device folder
MediaDeviceCopier.exe list-files -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE"

# List only JPEG files and print full device paths
MediaDeviceCopier.exe lf -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE" -f "\.(jpg|jpeg)$" --full-path
```

---

### download-files

Downloads files from an MTP device folder to a Windows folder.

- **Command**: `download-files`
- **Alias**: `d`

Required options:

- `-n`, `--device-name`: the MTP device name.
- `-s`, `--source-folder`: the device folder to copy from.
- `-t`, `--target-folder`: the Windows folder to copy into.

Common optional options:

- `-se`, `--skip-existing` `[true|false]`: skip files already at the destination (default: `true`). Pass `false` to overwrite existing files.
- `-r`, `--copy-recursive` `[true|false]`: recurse into subfolders (default: `false`). The flag alone (`-r`) implies `true`.
- `-mv`, `--move`: delete source after successful transfer.
- `-sf`, `--filter-subfolders`: .NET regex filter for subfolder names (used during recursion).
- `-f`, `--filter-files`: .NET regex filter for file names.

Examples:

```powershell
# Copy pictures recursively and skip already-copied images
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage" -t "D:\MyPictureFolder" -r

# Move (download then delete from device) all videos after archiving
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE" -t "D:\Archive" -r --move

# Copy only MP4 files from a flat folder (non-recursive)
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE" -t "D:\Videos" -f "\.mp4$"

# Recursive copy: only subfolders starting with 2025, only JPG/PNG files
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage" -t "D:\MyPictureFolder" -r -sf "^2025.*" -f "\.(jpg|png)$"

# Force overwrite existing files (disable skip-existing)
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage\DCIM" -t "D:\Photos" --skip-existing false
```

---

### upload-files

Uploads files from a Windows folder to an MTP device folder.

- **Command**: `upload-files`
- **Alias**: `u`

Required options:

- `-n`, `--device-name`: the MTP device name.
- `-s`, `--source-folder`: the Windows folder to copy from.
- `-t`, `--target-folder`: the device folder to copy into.

Common optional options:

- `-se`, `--skip-existing` `[true|false]`: skip files already at the destination (default: `true`). Pass `false` to overwrite existing files.
- `-r`, `--copy-recursive` `[true|false]`: recurse into subfolders (default: `false`). The flag alone (`-r`) implies `true`.
- `-mv`, `--move`: delete source after successful transfer.
- `-sf`, `--filter-subfolders`: .NET regex filter for subfolder names (used during recursion).
- `-f`, `--filter-files`: .NET regex filter for file names.

Examples:

```powershell
# Upload only PDFs
MediaDeviceCopier.exe upload-files -n "Android Device" -s "C:\Documents" -t "Internal Storage\Documents" -r -f "\.pdf$"
```

---

## Filtering

The filtering system uses two independent regex patterns:

1. **Subfolder filters** (`-sf`, `--filter-subfolders`) are applied during folder recursion **before** descending into subfolders.
2. **File filters** (`-f`, `--filter-files`) are applied to the file list within each processed folder.

Notes:

- Regex syntax is standard **.NET regular expressions**.
- `list-files` filters by **file name**, not the full path.
- If no filters are specified, all folders/files are included.

---

## Common behaviors and defaults

- **Device name discovery**: run `list-devices` first to get the exact name to pass to `-n`.
- **Defaults**:
  - `--skip-existing` behaves as **true** when omitted.
  - `--copy-recursive` behaves as **false** when omitted.
  - `--move` behaves as **false** when omitted.
- **Boolean options** (`--skip-existing`, `--copy-recursive`): can be passed as a bare flag (`-r` = true) or with an explicit value (`--skip-existing false`).
- **Folder creation**:
  - On **download**, if the Windows target folder does not exist and `--copy-recursive` is enabled, the folder is created.
  - On **upload**, if the device target folder does not exist and `--copy-recursive` is enabled, the folder is created.
- **Version**:

```powershell
MediaDeviceCopier.exe --version
```

---

## Troubleshooting

### Device not found

If you see “Device not found”, run:

```powershell
MediaDeviceCopier.exe list-devices
```

Then copy/paste the device name exactly into `-n`.

### Folder not found

- If a device folder path is wrong, the command will fail.
- If a Windows folder path is wrong:
  - with `--copy-recursive` enabled, download may create missing target folders
  - otherwise the command exits

### Invalid regex

If a filter regex is invalid, the CLI will reject it. Start simple and escape backslashes correctly in your shell.

### Unsupported file types

Some device files may not be transferable via MTP; those are reported as skipped.

### Failed files and exit code

If a file cannot be downloaded (for example, the device reports an object with no name, or every download strategy fails), it is reported as `FAILED (<reason>)`, the run continues with the remaining files, and a summary of failed files and folders is printed at the end. The exit code is `0` when everything succeeded and `1` if any file or folder failed, so scripts can detect an incomplete transfer.

### Empty folders and transient device errors

- Empty device folders are treated as having no files; they are not an error.
- Some devices (notably iPhones) intermittently fail folder enumeration with errors such as `0x8007000D` ("The data is invalid"). These are retried a few times with a short delay. If a folder still cannot be read during a recursive copy, it is reported, the copy continues with the next folder, and the run exits with code `1`.

---

## Architecture (resilient downloads)

MTP transfers can fail for certain files due to device firmware quirks, timing issues, or protocol limitations. MediaDeviceCopier uses a **multi-strategy download pipeline** and detailed diagnostics to make downloads more resilient.

Implementation details are documented in [ARCHITECTURE_MTP_STRATEGIES.md](ARCHITECTURE_MTP_STRATEGIES.md).

---

## Building from source

Requires the .NET 10 SDK on Windows (the app targets `net10.0-windows`).

```powershell
# Build
dotnet build -c Release

# Run the mocked test suite
dotnet test MediaDeviceCopier.Tests.Mocked/MediaDeviceCopier.Tests.Mocked.csproj

# Produce the single-file executable (framework-dependent, win-x64)
dotnet publish MediaDeviceCopier/MediaDeviceCopier.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
# -> publish/MediaDeviceCopier.exe
```

> **Note:** `MediaDeviceCopier.Tests.RealDevice` is a separate suite that requires a
> physical MTP device connected to the machine, so it is not run by `dotnet test` above
> or in CI. Run it manually when validating against real hardware.

The application version comes from a single source: the `<Version>` element in
[MediaDeviceCopier/MediaDeviceCopier.csproj](MediaDeviceCopier/MediaDeviceCopier.csproj).
`AssemblyVersion`, `FileVersion`, and the version shown by `--version` / `--help` are all
derived from it, so a release only requires changing that one value.

---

## Continuous integration

Two GitHub Actions workflows run automatically on every pull request and on pushes to `main`:

- **CI** ([.github/workflows/ci.yml](.github/workflows/ci.yml)) — restores, builds in Release,
  and runs the mocked test suite on `windows-latest`.
- **Lint** ([.github/workflows/lint.yml](.github/workflows/lint.yml)) — fails if any file is
  committed with CRLF line endings (line endings are normalized to LF via
  [.gitattributes](.gitattributes)). If this fails, run `git add --renormalize .` and commit.

[Dependabot](.github/dependabot.yml) opens weekly PRs for NuGet and GitHub Actions updates.

---

## Releasing

Releases are produced by the **Release** workflow
([.github/workflows/release.yml](.github/workflows/release.yml)), which triggers on any pushed
tag matching `v*`. To cut a release:

1. **Bump the version.** Edit `<Version>` in
   [MediaDeviceCopier/MediaDeviceCopier.csproj](MediaDeviceCopier/MediaDeviceCopier.csproj)
   (this is the only place to change it), commit, and merge to `main`.
2. **Tag and push.** The tag version must match `<Version>` exactly (with a `v` prefix) —
   the workflow verifies this and fails the build on a mismatch:

   ```powershell
   git checkout main
   git pull
   git tag v0.8.0
   git push origin v0.8.0
   ```

3. **The workflow then** publishes `MediaDeviceCopier.exe`, generates release notes
   (categorized via [.github/release.yml](.github/release.yml)), and creates a **draft**
   GitHub Release with the exe attached.
4. **Review and publish.** Open the draft release on GitHub, review the auto-generated notes
   (add any behavior-change callouts), confirm "Set as the latest release", and click
   **Publish release**.

> **Tip:** When a PR closes multiple issues, give each one its own closing keyword —
> `Closes #20, closes #21, closes #22`. Writing `Closes #20, #21, #22` only closes the first.

---

## License

MIT License. See [LICENSE](LICENSE) for details.

---

## Contributing

Contributions, issues, and feature requests are welcome!
