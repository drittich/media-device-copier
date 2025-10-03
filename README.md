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

## Resilient Download Architecture

MediaDeviceCopier implements a sophisticated multi-strategy download system to handle problematic MTP file transfers reliably. Some file types (particularly THM thumbnails, WAV audio, and certain metadata files) can fail with standard MTP download methods due to device firmware quirks, timing issues, or protocol limitations.

### Download Strategy Pipeline

When downloading files, the system automatically tries multiple strategies in sequence until one succeeds:

1. **Standard Download** - Direct MTP transfer using the MediaDevices library
2. **Stream Retry** - Retry with a small delay (100ms) for timing-sensitive files
3. **Chunked Retry** - Retry with longer delay (200ms) for buffer-related issues
4. **Metadata Probe** - Extended delay retry (500ms) as a last resort

Each strategy logs detailed diagnostics including:
- Strategy name
- Execution time in milliseconds
- COM error codes (HResult) for failures
- Success/failure status

### Universal Application

The strategy pipeline applies to **all file types** universally, without hardcoded extension checks. This extension-agnostic design ensures:

- Consistent behavior across all file types
- No special-case logic that could become outdated
- Easy extensibility for future device quirks
- Comprehensive diagnostic logging for troubleshooting

### File Classification

Files are automatically classified by extension for potential future optimizations:

- **Image**: jpg, jpeg, png, gif, bmp, tiff, raw, cr2, nef, arw, dng
- **Video**: mp4, mov, avi, mkv, wmv, m4v, mpg, mpeg, flv, webm, 3gp
- **Audio**: wav, mp3, flac, aac, m4a, wma, ogg, opus, alac
- **Metadata**: thm, lrv, xmp, sidecar
- **Document**: pdf, txt, doc, docx, xls, xlsx
- **Unknown**: all other extensions

This classification is currently informational but provides hooks for future strategy customization per file class.

### Failure Handling

If all strategies fail, the file is:
- Marked as `SkippedBecauseUnsupported`
- Logged with a warning message
- Not deleted from source (if using `--move`)
- Reported in the final summary

### Example Diagnostic Output

```
[Standard] COM-Error:0x80004005 (0ms)
[StreamRetry] COM-Error:0x80004005 (105ms)
[ChunkedRetry] Success (204ms)
```

This shows the Standard strategy failed immediately, StreamRetry failed after 105ms, and ChunkedRetry succeeded after 204ms.

### Extensibility

The strategy system is designed for easy extension:

- New strategies can be added to the collection
- Strategy order can be customized
- Per-file-class strategies can be implemented
- Custom timing delays can be configured

For implementation details, see [`ARCHITECTURE_MTP_STRATEGIES.md`](ARCHITECTURE_MTP_STRATEGIES.md).

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
