using System.CommandLine;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MediaDeviceCopier
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = SetupCommandHandler();
            return await rootCommand.InvokeAsync(args);
        }

        private static RootCommand SetupCommandHandler()
        {
            // create options
            var deviceNameOption = new Option<string>(
                new[] { "--device-name", "-n" },
                description: "The MTP device we'll be copying files to/from.")
            {
                IsRequired = true
            };

            var sourceFolderOption = new Option<string>(
                new[] { "--source-folder", "-s" },
                description: "The folder we'll be copying files from.")
            {
                IsRequired = true
            };

            var targetFolderOption = new Option<string>(
                new[] { "--target-folder", "-t" },
                description: "The folder we'll be copying files to.")
            {
                IsRequired = true
            };

            var skipExistingFilesOption = new Option<bool?>(
                new[] { "--skip-existing", "-se" },
                description: "Whether to skip existing files (default: true).");

            var copyRecursiveOption = new Option<bool?>(
                new[] { "--copy-recursive", "-r" },
                description: "Copy folders recursively (default: false).");

            var moveOption = new Option<bool>(
                new[] { "--move", "-mv" },
                description: "Delete source after successful transfer (move operation).");

            var filterSubfoldersOption = new Option<string>(
                new[] { "--filter-subfolders", "-sf" },
                description: "Optional: Include only subfolders which match the regular expression pattern (default: all).")
            {
                IsRequired = false
            };
            filterSubfoldersOption.AddValidator(context =>
            {
                if (context.Tokens.Count > 0)
                {
                    try { _ = new Regex(context.Tokens[0].Value); }
                    catch (Exception ex) { context.ErrorMessage = $"Invalid subfolder regex pattern: {ex.Message}"; }
                }
            });

            var filterFilesOption = new Option<string>(
                new[] { "--filter-files", "-f" },
                description: "Optional: Include only files which match the regular expression pattern (default: all).")
            {
                IsRequired = false
            };
            filterFilesOption.AddValidator(context =>
            {
                if (context.Tokens.Count > 0)
                {
                    try { _ = new Regex(context.Tokens[0].Value); }
                    catch (Exception ex) { context.ErrorMessage = $"Invalid file regex pattern: {ex.Message}"; }
                }
            });

            // create commands
            var rootCommand = new RootCommand("MediaDeviceCopier");

            var listDevicesCommand = new Command("list-devices", "List the available MTP devices.");
            listDevicesCommand.AddAlias("l");
            rootCommand.AddCommand(listDevicesCommand);

            var uploadCommand = new Command("upload-files", "Upload files to the MTP device.");
            uploadCommand.AddAlias("u");
            uploadCommand.AddOption(deviceNameOption);
            uploadCommand.AddOption(sourceFolderOption);
            uploadCommand.AddOption(targetFolderOption);
            uploadCommand.AddOption(skipExistingFilesOption);
            uploadCommand.AddOption(copyRecursiveOption);
            uploadCommand.AddOption(moveOption);
            uploadCommand.AddOption(filterSubfoldersOption);
            uploadCommand.AddOption(filterFilesOption);
            rootCommand.AddCommand(uploadCommand);

            var downloadCommand = new Command("download-files", "Download files from the MTP device.");
            downloadCommand.AddAlias("d");
            downloadCommand.AddOption(deviceNameOption);
            downloadCommand.AddOption(sourceFolderOption);
            downloadCommand.AddOption(targetFolderOption);
            downloadCommand.AddOption(skipExistingFilesOption);
            downloadCommand.AddOption(copyRecursiveOption);
            downloadCommand.AddOption(moveOption);
            downloadCommand.AddOption(filterSubfoldersOption);
            downloadCommand.AddOption(filterFilesOption);
            rootCommand.AddCommand(downloadCommand);

            // set handlers
            listDevicesCommand.SetHandler(ListDevices);

            uploadCommand.SetHandler(
                (string deviceName, string sourceFolder, string targetFolder, bool? skipExisting, bool? recursive, bool move, string? filterSubfolderPattern, string? filterFilePattern) =>
                {
                    CopyFiles("upload", deviceName, sourceFolder, targetFolder, skipExisting, recursive, filterSubfolderPattern, filterFilePattern, move);
                },
                deviceNameOption, sourceFolderOption, targetFolderOption, skipExistingFilesOption, copyRecursiveOption, moveOption, filterSubfoldersOption, filterFilesOption
            );

            downloadCommand.SetHandler(
                (string deviceName, string sourceFolder, string targetFolder, bool? skipExisting, bool? recursive, bool move, string? filterSubfolderPattern, string? filterFilePattern) =>
                {
                    CopyFiles("download", deviceName, sourceFolder, targetFolder, skipExisting, recursive, filterSubfolderPattern, filterFilePattern, move);
                },
                deviceNameOption, sourceFolderOption, targetFolderOption, skipExistingFilesOption, copyRecursiveOption, moveOption, filterSubfoldersOption, filterFilesOption
            );

            return rootCommand;
        }

        private static void CopyFiles(string mode, string deviceName, string sourceFolder, string targetFolder, bool? skipExisting, bool? recursive, string? filterSubfolderPattern, string? filterFilePattern, bool? move)
        {
            var sw = Stopwatch.StartNew();
            var fileCopyMode = mode == "download" ? FileCopyMode.Download : FileCopyMode.Upload;
            var isMove = move ?? false;

            using var device = GetDeviceByName(deviceName);

            // Subfolder filter
            Regex? filterSubfolderRegex = null;
            if (!string.IsNullOrEmpty(filterSubfolderPattern))
                filterSubfolderRegex = new Regex(filterSubfolderPattern);

            // File filter
            Regex? filterFileRegex = null;
            if (!string.IsNullOrEmpty(filterFilePattern))
                filterFileRegex = new Regex(filterFilePattern);

            // recursive copy?
            if (recursive ?? false)
            {
                var subFolders = fileCopyMode == FileCopyMode.Download
                    ? device.GetDirectories(sourceFolder)
                    : Directory.GetDirectories(sourceFolder);

                Array.Sort(subFolders);
                Console.WriteLine($"Found {subFolders.Length} subfolders.");

                foreach (var subFolderFullPath in subFolders)
                {
                    var dirInfo = new DirectoryInfo(subFolderFullPath);
                    var subFolderName = dirInfo.Name;
                    if (string.IsNullOrEmpty(subFolderName))
                        continue;

                    if (filterSubfolderRegex != null && !filterSubfolderRegex.IsMatch(subFolderName))
                    {
                        Console.WriteLine($"Skipping subfolder: {subFolderName}");
                        continue;
                    }

                    var subTargetFullPath = Path.Combine(targetFolder, subFolderName);
                    CopyFiles(mode, deviceName, subFolderFullPath, subTargetFullPath, skipExisting, recursive, filterSubfolderPattern, filterFilePattern, move);
                }
            }

            // Ensure connected
            if (!device.IsConnected)
                device.Connect();

            // Get files
            var files = fileCopyMode == FileCopyMode.Download
                ? device.GetFiles(sourceFolder)
                : Directory.GetFiles(sourceFolder);

            // Apply file filtering
            if (filterFileRegex != null)
                files = files.Where(fp => filterFileRegex.IsMatch(Path.GetFileName(fp))).ToArray();

            ValidateFolders(fileCopyMode, device, sourceFolder, targetFolder, recursive);

            Console.WriteLine($"Copying {files.Length:N0} files...");
            ulong bytesCopied = 0, bytesNotCopied = 0;

            foreach (var sourceFilePath in files.OrderBy(f => f))
            {
                var targetFilePath = Path.Combine(targetFolder, Path.GetFileName(sourceFilePath));
                Console.Write($"{sourceFilePath}...");

                var resultInfo = device.CopyFile(fileCopyMode, sourceFilePath, targetFilePath, skipExisting ?? true, isMove);
                if (resultInfo.CopyStatus == FileCopyStatus.SkippedBecauseAlreadyExists ||
                    resultInfo.CopyStatus == FileCopyStatus.SkippedBecauseUnsupported)
                    bytesNotCopied += resultInfo.Length;
                else
                    bytesCopied += resultInfo.Length;

                WriteCopyResult(resultInfo);
            }

            Console.WriteLine($"Done, copied {BytesToString(bytesCopied)}, skipped {BytesToString(bytesNotCopied)}");
            Console.WriteLine($"Elapsed time: {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
        }

        private static void ValidateFolders(FileCopyMode fileCopyMode, MtpDevice device, string sourceFolder, string targetFolder, bool? recursive)
        {
            var mtpFolder = fileCopyMode == FileCopyMode.Download ? sourceFolder : targetFolder;
            var windowsFolder = fileCopyMode == FileCopyMode.Download ? targetFolder : sourceFolder;

            if (!device.DirectoryExists(mtpFolder))
            {
                if (recursive == true && fileCopyMode == FileCopyMode.Upload)
                {
                    Console.WriteLine($"[{device.FriendlyName}] folder does not exist: {mtpFolder}. Creating...");
                    device.CreateDirectory(mtpFolder);
                }
                else
                {
                    Console.WriteLine($"[{device.FriendlyName}] folder does not exist: {mtpFolder}");
                    Environment.Exit(1);
                }
            }

            if (!Directory.Exists(windowsFolder))
            {
                Console.WriteLine($"Windows folder does not exist: {windowsFolder}");
                if (recursive == true && fileCopyMode == FileCopyMode.Download)
                {
                    Console.WriteLine("Creating folder...");
                    Directory.CreateDirectory(windowsFolder);
                }
                else
                {
                    Console.WriteLine();
                    Environment.Exit(1);
                }
            }
        }

        private static void WriteCopyResult(FileCopyResultInfo fileCopyResultInfo)
        {
            var suffix = fileCopyResultInfo.SourceDeleted ? " (moved)" : string.Empty;
            switch (fileCopyResultInfo.CopyStatus)
            {
                case FileCopyStatus.Copied:
                    Console.WriteLine($"copied{suffix}");
                    break;
                case FileCopyStatus.CopiedBecauseDateOrSizeMismatch:
                    Console.WriteLine($"copied (date or size mismatch){suffix}");
                    break;
                case FileCopyStatus.SkippedBecauseAlreadyExists:
                    Console.WriteLine("skipped (already exists)");
                    break;
                case FileCopyStatus.SkippedBecauseUnsupported:
                    Console.WriteLine("skipped (unsupported file type)");
                    break;
            }
        }

        private static MtpDevice GetDeviceByName(string deviceName)
        {
            var device = MtpDevice.GetByName(deviceName);
            if (device is null)
            {
                Console.WriteLine($"Device not found: {deviceName}");
                Console.WriteLine("Available devices are:");
                foreach (var d in MtpDevice.GetAll())
                    Console.WriteLine($"   {d.FriendlyName}");
                Environment.Exit(1);
            }

            try
            {
                device.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to device: {ex.Message}");
                Environment.Exit(1);
            }

            return device;
        }

        private static void ListDevices()
        {
            foreach (var device in MtpDevice.GetAll())
                Console.WriteLine($"Device: {device.FriendlyName}");
        }

        private static string BytesToString(ulong byteCount)
        {
            var longByteCount = (long)byteCount;
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (longByteCount == 0)
                return "0" + suf[0];
            long bytes = System.Math.Abs(longByteCount);
            int place = Convert.ToInt32(System.Math.Floor(System.Math.Log(bytes, 1024)));
            double num = System.Math.Round(bytes / System.Math.Pow(1024, place), 1);
            return (System.Math.Sign(longByteCount) * num).ToString() + suf[place];
        }
    }
}