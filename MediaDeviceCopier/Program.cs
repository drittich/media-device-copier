using System.CommandLine;

namespace MediaDeviceCopier
{
	internal class Program
	{
		static int Main(string[] args)
		{
			var rootCommand = SetupCommandHandler();

			return rootCommand.InvokeAsync(args).Result;
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
			var fileCopyMode = mode == "download" ? FileCopyMode.Download : FileCopyMode.Upload;

			var device = GetDeviceByName(deviceName);

			if (mode == "download")
			{
				if (!Directory.Exists(targetFolder))
				{
					Console.WriteLine($"Target folder does not exist: {targetFolder}");
					Environment.Exit(1);
				}

				var files = device.GetFiles(sourceFolder);
				var count = files.Length;
				Console.WriteLine($"Copying {count:N0} files...");

				foreach (var file in files.OrderBy(d => d))
				{
					var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));
					Console.Write($"{file}...");

					var copyResult = device.CopyFile(fileCopyMode, file, targetFile, skipExisting ??= true);
					if (copyResult == FileCopyStatus.Copied)
					{
						Console.WriteLine();
					}
					else if (copyResult == FileCopyStatus.CopiedBecauseDateOrSizeMismatch)
					{
						Console.WriteLine($"overwriting because date or size mismatch");
					}
					else if (copyResult == FileCopyStatus.SkippedBecauseAlreadyExists)
					{
						Console.WriteLine($"skipped becuse already exists");
					}
				}
				Console.WriteLine($"Done");
			}
			if (mode == "upload")
			{
				// TODO: check whether target folder exists

				var files = Directory.GetFiles(sourceFolder);
				var count = files.Length;
				Console.WriteLine($"Copying {count:N0} files...");

				foreach (var file in files.OrderBy(d => d))
				{
					var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));
					Console.Write($"{file}...");

					var copyResult = device.CopyFile(fileCopyMode, file, targetFile, skipExisting ??= true);
					if (copyResult == FileCopyStatus.Copied)
					{
						Console.WriteLine();
					}
					else if (copyResult == FileCopyStatus.CopiedBecauseDateOrSizeMismatch)
					{
						Console.WriteLine($"overwriting because date or size mismatch");
					}
					else if (copyResult == FileCopyStatus.SkippedBecauseAlreadyExists)
					{
						Console.WriteLine($"skipped becuse already exists");
					}
				}
				Console.WriteLine($"Done");
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
	}
}