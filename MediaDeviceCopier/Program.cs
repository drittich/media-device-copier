using System.CommandLine;


using MediaDevices;

namespace MediaDeviceCopier
{
	internal class Program
	{
		static int Main(string[] args)
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
			rootCommand.AddCommand(uploadCommand);

			var downloadCommand = new Command("download-files", "Download files from the MTP device.");
			downloadCommand.AddAlias("d");
			downloadCommand.AddOption(deviceNameOption);
			downloadCommand.AddOption(sourceFolderOption);
			downloadCommand.AddOption(targetFolderOption);
			rootCommand.AddCommand(downloadCommand);

			// set handlers
			listDevicesCommand.SetHandler(ListDevices);

			uploadCommand.SetHandler((deviceName, sourceFolder, targetFolder) =>
				{
					CopyFiles("upload", deviceName!, sourceFolder, targetFolder);
				},
				deviceNameOption, sourceFolderOption, targetFolderOption);

			downloadCommand.SetHandler((deviceName, sourceFolder, targetFolder) =>
				{
					CopyFiles("download", deviceName!, sourceFolder, targetFolder);
				},
				deviceNameOption, sourceFolderOption, targetFolderOption);

			return rootCommand.InvokeAsync(args).Result;
		}

		private static void CopyFiles(string mode, string deviceName, string sourceFolder, string targetFolder)
		{
			var devices = MediaDevice.GetDevices().OrderBy(d => d.FriendlyName).ToList();
			var device = devices.FirstOrDefault(d => d.FriendlyName.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase));

			if (device is null)
			{
				Console.WriteLine($"Device not found: {deviceName}");
				Console.WriteLine($"Available devices are:");
				foreach (var d in devices)
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
					Console.WriteLine(file);
					device.DownloadFile(file, Path.Combine(targetFolder, Path.GetFileName(file)));
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
					Console.WriteLine(file);
					device.UploadFile(file, Path.Combine(targetFolder, Path.GetFileName(file)));
				}
				Console.WriteLine($"Done");
			}
		}

		private static void ListDevices()
		{
			var devices = MediaDevice.GetDevices().ToList();

			foreach (var device in devices)
			{
				Console.WriteLine($"Device: {device.FriendlyName}");
			}
		}
	}
}