# media-device-copier
**Media Device Copier** is a Windows command-line utility for copying files to and from phones and other devices connected via MTP (Media Transfer Protocol).

Easily list connected devices, upload files to your device, or download files from your device—all from the command line.


---

## Features

- **List devices:** See all connected MTP devices.
- **Upload files:** Transfer files or folders to your device.
- **Download files:** Copy files or folders from your device to your PC.
- **Skip existing files:** Optionally avoid re-copying files.
- **Move files:** Use `--move` to delete the source after a successful transfer (download or upload).
- **Recursive copy:** Copy entire folder structures if needed.
- **Subfolder and file filtering:** Use regex patterns to include only matching subfolders and files.
```

PS C:\Program Files\MediaDeviceCopier> .\MediaDeviceCopier.exe -h
Description:
  MediaDeviceCopier

Usage:
  MediaDeviceCopier [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  l, list-devices    List the available MTP devices.
  u, upload-files    Upload files to the MTP device.
  d, download-files  Download files from the MTP device.
```

Use the `list-devices` command to determine the device name you need to pass for the following operations.

Downloading files options:

```
PS C:\Program Files\MediaDeviceCopier> .\MediaDeviceCopier.exe d -h
Description:
  Download files from the MTP device.

Usage:
  MediaDeviceCopier download-files [options]

Options:
  -n, --device-name <device-name> (REQUIRED)      The MTP device we'll be copying files to/from.
  -s, --source-folder <source-folder> (REQUIRED)  The folder we'll be copying files from.
  -t, --target-folder <target-folder> (REQUIRED)  The folder we'll be copying files to.
  -se, --skip-existing                            Whether to skip existing files (default: true).
  -r, --copy-recursive                            Copy folders recursive (default: false).
  -mv, --move                                     Delete source after successful transfer (move).
  -sf, --filter-subfolders <filter-subfolders>  Optional: Include only subfolders which match the regular expression pattern. Default: all
  -f, --filter-files <filter-files>  Optional: Include only files which match the regular expression pattern. Default: all
  -?, -h, --help                                  Show help and usage information
```

Uploading files options:

```
PS C:\Program Files\MediaDeviceCopier> .\MediaDeviceCopier.exe u -h
Description:
  Upload files to the MTP device.

Usage:
  MediaDeviceCopier upload-files [options]

Options:
  -n, --device-name <device-name> (REQUIRED)      The MTP device we'll be copying files to/from.
  -s, --source-folder <source-folder> (REQUIRED)  The folder we'll be copying files from.
  -t, --target-folder <target-folder> (REQUIRED)  The folder we'll be copying files to.
  -se, --skip-existing                            Whether to skip existing files (default: true).
  -r, --copy-recursive                            Copy folders recursive (default: false).
  -mv, --move                                     Delete source after successful transfer (move).
  -sf, --filter-subfolders <filter-subfolders>  Optional: Include only subfolders which match the regular expression pattern. Default: all
  -f, --filter-files <filter-files>  Optional: Include only files which match the regular expression pattern. Default: all
  -?, -h, --help                                  Show help and usage information
```
## Examples

### Basic Operations

Copy pictures from an iPhone recursively and skip already-copied images:
```
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage" -t "D:\MyPictureFolder" -se -r
```

Move (download then delete from device) all videos after archiving:
```
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE" -t "D:\Archive" -se -r --move
```

### Filtering Examples

Copy only MP4 files from a flat folder (non-recursive):
```
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE" -t "D:\Videos" -se -f "\.mp4$"
```

Recursive copy filtering subfolders starting with 2025 and copying only JPG/PNG files:
```
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage" -t "D:\MyPictureFolder" -se -r -sf "^2025.*" -f "\.(jpg|png)$"
```

Upload example using file filter to copy only PDF files:
```
MediaDeviceCopier.exe upload-files -n "Android Device" -s "C:\Documents" -t "Internal Storage\Documents" -se -r -f "\.pdf$"
```
### Version Information

The tool reports its version information using `--version` or `-h` flags.

```
PS C:\Program Files\MediaDeviceCopier> .\MediaDeviceCopier.exe --version
0.5.0
```

```
PS C:\Program Files\MediaDeviceCopier> .\MediaDeviceCopier.exe -h
Description:
  MediaDeviceCopier

Usage:
  MediaDeviceCopier [command] [options]
```


## Filtering

The filtering system uses two independent regex patterns that are evaluated in this order:

1. **Subfolder filters** (`-sf`) are applied during folder recursion before descending into subfolders
2. **File filters** (`-f`) are applied to the file list within each processed folder

Both filters are independent - you can use one or both together. If no filters are specified, all subfolders and files are included by default.

## Deprecated

**Note:** The previous `-p` / `--filter-subfolder-regex-pattern` option has been replaced by `-sf` / `--filter-subfolders`. Users must migrate to the new option.

## Tips

- Always use `list-devices` first to get the correct device name.
- Use double quotes around folder names if they contain spaces.
- Regular expressions in `-sf` and `-f` follow standard .NET regex syntax.

---

## License

MIT License. See [LICENSE](LICENSE) for details.

---

## Contributing

Contributions, issues, and feature requests are welcome!

---

## Support

If you encounter issues or need help, please open an issue on the repository.

---

*Happy copying!*
