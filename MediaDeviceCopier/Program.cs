using MediaDevices;

namespace MediaDeviceCopier
{
	internal class Program
	{
		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				ShowHelpInfo();
				Environment.Exit(0);
			}

			if (args[0] == "listdevices")
			{
				ListDevices();
			}

			else if (args[0] == "upload" || args[0] == "download")
			{
				CopyFiles(args[0], args[1], args[2], args[3]);
			}
			else
			{
				ShowHelpInfo();
				Environment.Exit(0);
			}

			Console.WriteLine($"Done");
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
			}
		}

		private static void ShowHelpInfo()
		{
			Console.WriteLine("MediaDeviceCopier v0.1");
			Console.WriteLine("Commands:");
			Console.WriteLine("   listdevices");
			Console.WriteLine("   download [device name] [source folder] [destination]");
			Console.WriteLine("   upload [device name] [source folder] [destination]");
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