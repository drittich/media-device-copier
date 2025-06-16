# media-device-copier
A Windows command-line utility for copying files from phones and other media devices connected as MTP devices

You have command to list devices, upload files to the device, or download files from the device.

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
  -?, -h, --help                                  Show help and usage information
```
