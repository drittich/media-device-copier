using System.CommandLine;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
				["--device-name", "-n"],
				description: "The MTP device we'll be copying files to/from.")
			{
				IsRequired = true
			};

			var sourceFolderOption = new Option<string>(
				["--source-folder", "-s"],
				description: "The folder we'll be copying files from.")
			{
				IsRequired = true
			};

			var targetFolderOption = new Option<string>(
				["--target-folder", "-t"],
				description: "The folder we'll be copying files to.")
			{
				IsRequired = true
			};

			var skipExistingFilesOption = new Option<bool?>(
				["--skip-existing", "-se"],
				description: "Whether to skip existing files (default: true).");

			var copyRecursiveOption = new Option<bool?>(
				["--copy-recursive", "-r"],
				description: "Copy folders recursively (default: false).");

			var moveOption = new Option<bool>(
				["--move", "-mv"],
				description: "Delete source after successful transfer (move operation).");

			var FilterSubFolderPattern = new Option<string>(
				 ["--filter-subfolder-regex-pattern", "-p"],
				description: "Optional: Include only subfolders which match the regular expression pattern (default: copy all)."
				)
			{
				IsRequired = false
			};

			FilterSubFolderPattern.AddValidator(ValidateRegEx =>
			{
				try
				{
					Regex TempRegEx = new(ValidateRegEx.Tokens[0].Value);
				}
				catch (Exception Ex)
				{
					ValidateRegEx.ErrorMessage = $"Invalid regular expression. Error: {Ex.Message}";
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
			uploadCommand.AddOption(FilterSubFolderPattern);
			rootCommand.AddCommand(uploadCommand);

			var downloadCommand = new Command("download-files", "Download files from the MTP device.");
			downloadCommand.AddAlias("d");
			downloadCommand.AddOption(deviceNameOption);
			downloadCommand.AddOption(sourceFolderOption);
			downloadCommand.AddOption(targetFolderOption);
			downloadCommand.AddOption(skipExistingFilesOption);
			downloadCommand.AddOption(copyRecursiveOption);
			downloadCommand.AddOption(moveOption);
			downloadCommand.AddOption(FilterSubFolderPattern);
			rootCommand.AddCommand(downloadCommand);

			// set handlers
			listDevicesCommand.SetHandler(ListDevices);

			uploadCommand.SetHandler((deviceName, sourceFolder, targetFolder, skipExisting, copyRecursiveOptionValue, move, filterSubFolderPattern) =>
			{
				CopyFiles("upload", deviceName!, sourceFolder, targetFolder, skipExisting, copyRecursiveOptionValue, filterSubFolderPattern, move);
			},
				deviceNameOption, sourceFolderOption, targetFolderOption, skipExistingFilesOption, copyRecursiveOption, moveOption, FilterSubFolderPattern);

			downloadCommand.SetHandler((deviceName, sourceFolder, targetFolder, skipExisting, copyRecursiveOptionValue, move, filterSubFolderPattern) =>
			{
				CopyFiles("download", deviceName!, sourceFolder, targetFolder, skipExisting, copyRecursiveOptionValue, filterSubFolderPattern, move);
			},
				deviceNameOption, sourceFolderOption, targetFolderOption, skipExistingFilesOption, copyRecursiveOption, moveOption, FilterSubFolderPattern);
			return rootCommand;
		}

		private static void CopyFiles(string mode, string deviceName, string sourceFolder, string targetFolder, bool? skipExisting, bool? recursive, string? FilterSubFolderPattern, bool? move)
		{
			var sw = Stopwatch.StartNew();
			var fileCopyMode = mode == "download" ? FileCopyMode.Download : FileCopyMode.Upload;
			var isMove = move ?? false;

			using var device = GetDeviceByName(deviceName);

			// Subfolder filter by regexpression
			Regex? FilterFolderPattern = null;
			if (!string.IsNullOrEmpty(FilterSubFolderPattern))
			{
				FilterFolderPattern = new Regex(FilterSubFolderPattern);
			}

			// recursive copy?
			if (recursive ??= false)
			{
				string[] SubFolders;
				if (fileCopyMode == FileCopyMode.Download)
				{
					SubFolders = device.GetDirectories(sourceFolder);
				}
				else
				{
					SubFolders = [.. Directory.GetDirectories(sourceFolder)];
				}
				Array.Sort(SubFolders);
				Debug.WriteLine($"Found {SubFolders.Length} windows subdirectories:{string.Join("\r\n", SubFolders)}");
				Console.WriteLine($"Found {SubFolders.Length} device subdirectories.");
				// Console.WriteLine($"Found {SubFolders.Length} device subdirectories:{string.Join("\r\n", SubFolders)}");

				foreach (string SubFolderFullPath in SubFolders)
				{
					DirectoryInfo Directory = new(SubFolderFullPath);
					string? SubFolder = Directory.Name;
					if (!string.IsNullOrEmpty(SubFolder))
					{
						// Skip on pattern matching...
						if (FilterFolderPattern != null)
						{
							if (!FilterFolderPattern.Match(SubFolder).Success)
							{
								Console.WriteLine($"Skipping {SubFolderFullPath}");
								continue;
							}
						}
						string SubTargetFullPath = Path.Combine(targetFolder, SubFolder);
						// Console.WriteLine($"SUBFULL {SubFolderFullPath} TARGETFULL: {SubTargetFullPath} TARGET:{targetFolder}  SUB:{SubFolder}");
						CopyFiles(mode, deviceName, SubFolderFullPath, SubTargetFullPath, skipExisting, recursive, FilterSubFolderPattern, move);
					}
				}
			}

			// Disconnect after copy files recursive
			if (!device.IsConnected)
				device.Connect();

			string[] files = fileCopyMode is FileCopyMode.Download ? device.GetFiles(sourceFolder) : Directory.GetFiles(sourceFolder);

			ValidateFolders(fileCopyMode, device, sourceFolder, targetFolder, recursive);

			var count = files.Length;
			ulong bytesCopied = 0;
			ulong bytesNotCopied = 0;
			Console.WriteLine($"Copying {count:N0} files...");

			foreach (var sourceFilePath in files.OrderBy(d => d))
			{
				var targetFilePath = Path.Combine(targetFolder, Path.GetFileName(sourceFilePath));
				Console.Write($"{sourceFilePath}...");

				var fileCopyResultInfo = device.CopyFile(fileCopyMode, sourceFilePath, targetFilePath, skipExisting ??= true, isMove);
				if (fileCopyResultInfo.CopyStatus == FileCopyStatus.SkippedBecauseAlreadyExists ||
					fileCopyResultInfo.CopyStatus == FileCopyStatus.SkippedBecauseUnsupported)
				{
					bytesNotCopied += fileCopyResultInfo.Length;
				}
				else
				{
					bytesCopied += fileCopyResultInfo.Length;
				}

				WriteCopyResult(fileCopyResultInfo);
			}
			Console.WriteLine($"Done, copied {BytesToString(bytesCopied)}, skipped {BytesToString(bytesNotCopied)}");
			Console.WriteLine($"Elapsed time: {sw.Elapsed:hh\\:mm\\:ss\\.ff}");
		}

		private static void ValidateFolders(FileCopyMode fileCopyMode, MtpDevice device, string sourceFolder, string targetFolder, bool? recursive)
		{
			string mtpFolder = fileCopyMode is FileCopyMode.Download ? sourceFolder : targetFolder;
			string windowsFolder = fileCopyMode is FileCopyMode.Download ? targetFolder : sourceFolder;

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
			string suffix = fileCopyResultInfo.SourceDeleted ? " (moved)" : string.Empty;
			if (fileCopyResultInfo.CopyStatus == FileCopyStatus.Copied)
			{
				Console.WriteLine($"copied{suffix}");
			}
			else if (fileCopyResultInfo.CopyStatus == FileCopyStatus.CopiedBecauseDateOrSizeMismatch)
			{
				Console.WriteLine($"copied (date or size mismatch){suffix}");
			}
			else if (fileCopyResultInfo.CopyStatus == FileCopyStatus.SkippedBecauseAlreadyExists)
			{
				Console.WriteLine($"skipped (already exists)");
			}
			else if (fileCopyResultInfo.CopyStatus == FileCopyStatus.SkippedBecauseUnsupported)
			{
				Console.WriteLine($"skipped (unsupported file type)");
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
			string[] suf = ["B", "KB", "MB", "GB", "TB", "PB", "EB"]; // Longs run out around EB
			if (longByteCount == 0)
				return "0" + suf[0];
			long bytes = Math.Abs(longByteCount);
			int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
			double num = Math.Round(bytes / Math.Pow(1024, place), 1);
			return (Math.Sign(longByteCount) * num).ToString() + suf[place];
		}
	}
}