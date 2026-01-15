# Media Device Copier

**Media Device Copier** is a Windows command-line utility for copying files to and from phones and other devices connected via MTP (Media Transfer Protocol).

Use it to:

- list connected devices
- list files on a device folder
- download from device to PC
- upload from PC to device

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Quick start](#quick-start)
- [Commands](#commands)
  - [list-devices](#list-devices)
  - [list-files](#list-files)
  - [download-files](#download-files)
  - [upload-files](#upload-files)
- [Filtering](#filtering)
- [Common behaviors and defaults](#common-behaviors-and-defaults)
- [Troubleshooting](#troubleshooting)
- [Architecture (resilient downloads)](#architecture-resilient-downloads)
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

- Windows (the app targets `net9.0-windows`).
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

- `-se`, `--skip-existing`: whether to skip existing files (default behavior is to skip).
- `-r`, `--copy-recursive`: recurse into subfolders (default is off).
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

- `-se`, `--skip-existing`: whether to skip existing files (default behavior is to skip).
- `-r`, `--copy-recursive`: recurse into subfolders (default is off).
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

---

## Architecture (resilient downloads)

MTP transfers can fail for certain files due to device firmware quirks, timing issues, or protocol limitations. MediaDeviceCopier uses a **multi-strategy download pipeline** and detailed diagnostics to make downloads more resilient.

Implementation details are documented in [ARCHITECTURE_MTP_STRATEGIES.md](ARCHITECTURE_MTP_STRATEGIES.md).

---

## License

MIT License. See [LICENSE](LICENSE) for details.

---

## Contributing

Contributions, issues, and feature requests are welcome!
