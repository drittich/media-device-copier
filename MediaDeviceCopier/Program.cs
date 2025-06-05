using System.CommandLine;
using System.Diagnostics;

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
			rootCommand.AddCommand(uploadCommand);

			var downloadCommand = new Command("download-files", "Download files from the MTP device.");
			downloadCommand.AddAlias("d");
			downloadCommand.AddOption(deviceNameOption);
			downloadCommand.AddOption(sourceFolderOption);
			downloadCommand.AddOption(targetFolderOption);
			downloadCommand.AddOption(skipExistingFilesOption);
			rootCommand.AddCommand(downloadCommand);

			// set handlers
			listDevicesCommand.SetHandler(ListDevices);

			uploadCommand.SetHandler((deviceName, sourceFolder, targetFolder, skipExisting) =>
			{
				CopyFiles("upload", deviceName!, sourceFolder, targetFolder, skipExisting);
			},
				deviceNameOption, sourceFolderOption, targetFolderOption, skipExistingFilesOption);

			downloadCommand.SetHandler((deviceName, sourceFolder, targetFolder, skipExisting) =>
			{
				CopyFiles("download", deviceName!, sourceFolder, targetFolder, skipExisting);
			},
				deviceNameOption, sourceFolderOption, targetFolderOption, skipExistingFilesOption);
			return rootCommand;
		}

		private static void CopyFiles(string mode, string deviceName, string sourceFolder, string targetFolder, bool? skipExisting)
		{
			var sw = Stopwatch.StartNew();
			var fileCopyMode = mode == "download" ? FileCopyMode.Download : FileCopyMode.Upload;

			using var device = GetDeviceByName(deviceName);
			string[] files = fileCopyMode is FileCopyMode.Download ? device.GetFiles(sourceFolder) : Directory.GetFiles(sourceFolder);

			ValidateFolders(fileCopyMode, device, sourceFolder, targetFolder);

			var count = files.Length;
			ulong bytesCopied = 0;
			ulong bytesNotCopied = 0;
			Console.WriteLine($"Copying {count:N0} files...");

			foreach (var sourceFilePath in files.OrderBy(d => d))
			{
				var targetFilePath = Path.Combine(targetFolder, Path.GetFileName(sourceFilePath));
				Console.Write($"{sourceFilePath}...copying");

				var fileCopyResultInfo = device.CopyFile(fileCopyMode, sourceFilePath, targetFilePath, skipExisting ??= true);
				if (fileCopyResultInfo.CopyStatus == FileCopyStatus.SkippedBecauseAlreadyExists)
				{
					bytesNotCopied += fileCopyResultInfo.Length;
				}
				else
				{
					bytesCopied += fileCopyResultInfo.Length;
				}

				// erase the word "copying" 
				Console.CursorLeft -= 7;

				WriteCopyResult(fileCopyResultInfo);
			}
			Console.WriteLine($"Done, copied {BytesToString(bytesCopied)}, skipped {BytesToString(bytesNotCopied)}");
			Console.WriteLine($"Elapsed time: {sw.Elapsed.ToString(@"hh\:mm\:ss\.ff")}");
		}

		private static void ValidateFolders(FileCopyMode fileCopyMode, MtpDevice device, string sourceFolder, string targetFolder)
		{
			string mtpFolder = fileCopyMode is FileCopyMode.Download ? sourceFolder : targetFolder;
			string windowsFolder = fileCopyMode is FileCopyMode.Download ? targetFolder : sourceFolder;

			if (!device.DirectoryExists(mtpFolder))
			{
				Console.WriteLine($"[{device.FriendlyName}] folder does not exist: {mtpFolder}");
				Environment.Exit(1);
			}
			if (!Directory.Exists(windowsFolder))
			{
				Console.WriteLine($"Windows folder does not exist: {windowsFolder}");
				Environment.Exit(1);
			}
		}

		private static void WriteCopyResult(FileCopyResultInfo fileCopyResultInfo)
		{
			if (fileCopyResultInfo.CopyStatus == FileCopyStatus.Copied)
			{
				Console.WriteLine("copied  ");
			}
			else if (fileCopyResultInfo.CopyStatus == FileCopyStatus.CopiedBecauseDateOrSizeMismatch)
			{
				Console.WriteLine($"copied (date or size mismatch)");
			}
			else if (fileCopyResultInfo.CopyStatus == FileCopyStatus.SkippedBecauseAlreadyExists)
			{
				Console.WriteLine($"skipped (already exists)");
			}
		}

		private static MtpDevice GetDeviceByName(string deviceName)
		{
			var device = MtpDevice.GetByName(deviceName);

			if (device is null)
			{
				Console.WriteLine($"Device not found: {deviceName}");
				Console.WriteLine($"Available devices are:");
				foreach (var d in MtpDevice.GetAll())
				{
					Console.WriteLine($"   {d.FriendlyName}");
				}
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
			var devices = MtpDevice.GetAll();

			foreach (var device in devices)
			{
				Console.WriteLine($"Device: {device.FriendlyName}");
			}
		}

		static string BytesToString(ulong byteCount)
		{
			var longByteCount = (long)byteCount;
			string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
			if (longByteCount == 0)
				return "0" + suf[0];
			long bytes = Math.Abs(longByteCount);
			int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
			double num = Math.Round(bytes / Math.Pow(1024, place), 1);
			return (Math.Sign(longByteCount) * num).ToString() + suf[place];
		}
	}
}