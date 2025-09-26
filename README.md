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
- **Folder filtering:** Use regex patterns to include only matching subfolders.
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
  -p, --filter-subfolder-regex-pattern <filter-subfolder-regex-pattern>  Optional: Include only subfolders which matches the regular expression pattern. Default copy all subfolders
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
  -p, --filter-subfolder-regex-pattern <filter-subfolder-regex-pattern>  Optional: Include only subfolders which matches the regular expression pattern. Default copy all subfolders
  -?, -h, --help                                  Show help and usage information
```
## Examples

Copy pictures from an iPhone recursively and skip already-copied images:
```
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage" -t "D:\MyPictureFolder" -se -r
```

Copy all pictures from an iPhone recursively, skip already copied images and only copy folders beginning with 2025:
```
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage" -t D:\MyPictureFolder" -se -r -p "^2025.*"
```

Move (download then delete from device) all videos after archiving:
```
MediaDeviceCopier.exe download-files -n "Apple iPhone" -s "Internal Storage\DCIM\100APPLE" -t "D:\Archive" -se -r --move
```

## Tips

- Always use `list-devices` first to get the correct device name.
- Use double quotes around folder names if they contain spaces.
- Regular expressions in `-p` follow standard .NET regex syntax.

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
