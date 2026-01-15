using System.CommandLine;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MediaDeviceCopier
{
	public class Program
	{
		internal const string Version = "0.6.0";

		public static async Task<int> Main(string[] args)
		{
			var rootCommand = SetupCommandHandler();
			return await rootCommand.Parse(args).InvokeAsync();
		}

		private static RootCommand SetupCommandHandler()
		{
			// create options
			var deviceNameOption = new Option<string>(
				"--device-name",
				new[] { "-n" })
			{
				Description = "The MTP device we'll be copying files to/from.",
				Required = true
			};

			var sourceFolderOption = new Option<string>(
				"--source-folder",
				new[] { "-s" })
			{
				Description = "The folder we'll be copying files from.",
				Required = true
			};

			var targetFolderOption = new Option<string>(
				"--target-folder",
				new[] { "-t" })
			{
				Description = "The folder we'll be copying files to.",
				Required = true
			};

			var skipExistingFilesOption = new Option<bool?>(
				"--skip-existing",
				new[] { "-se" })
			{
				Description = "Whether to skip existing files (default: true)."
			};

			var copyRecursiveOption = new Option<bool?>(
				"--copy-recursive",
				new[] { "-r" })
			{
				Description = "Copy folders recursively (default: false)."
			};

			var moveOption = new Option<bool>(
				"--move",
				new[] { "-mv" })
			{
				Description = "Delete source after successful transfer (move operation)."
			};

			var filterSubfoldersOption = new Option<string?>(
				"--filter-subfolders",
				new[] { "-sf" })
			{
				Description = "Optional: Include only subfolders which match the regular expression pattern (default: all)."
			};
			filterSubfoldersOption.Validators.Add(result =>
			{
				if (result.Tokens.Count > 0)
				{
					try { _ = new Regex(result.Tokens[0].Value); }
					catch (Exception ex) { result.AddError($"Invalid subfolder regex pattern: {ex.Message}"); }
				}
			});

			var filterFilesOption = new Option<string?>(
				"--filter-files",
				new[] { "-f" })
			{
				Description = "Optional: Include only files which match the regular expression pattern (default: all)."
			};
			filterFilesOption.Validators.Add(result =>
			{
				if (result.Tokens.Count > 0)
				{
					try { _ = new Regex(result.Tokens[0].Value); }
					catch (Exception ex) { result.AddError($"Invalid file regex pattern: {ex.Message}"); }
				}
			});

			var fullPathOption = new Option<bool>(
				"--full-path",
				Array.Empty<string>())
			{
				Description = "Print full device paths instead of file names."
			};

			// create commands
			var rootCommand = new RootCommand($"MediaDeviceCopier v{Version}");

			var listDevicesCommand = new Command("list-devices", "List the available MTP devices.");
			listDevicesCommand.Aliases.Add("l");
			rootCommand.Add(listDevicesCommand);

			var listFilesCommand = new Command("list-files", "List files in a device folder.");
			listFilesCommand.Aliases.Add("lf");
			listFilesCommand.Add(deviceNameOption);
			listFilesCommand.Add(sourceFolderOption);
			listFilesCommand.Add(filterFilesOption);
			listFilesCommand.Add(fullPathOption);
			rootCommand.Add(listFilesCommand);

			var uploadCommand = new Command("upload-files", "Upload files to the MTP device.");
			uploadCommand.Aliases.Add("u");
			uploadCommand.Add(deviceNameOption);
			uploadCommand.Add(sourceFolderOption);
			uploadCommand.Add(targetFolderOption);
			uploadCommand.Add(skipExistingFilesOption);
			uploadCommand.Add(copyRecursiveOption);
			uploadCommand.Add(moveOption);
			uploadCommand.Add(filterSubfoldersOption);
			uploadCommand.Add(filterFilesOption);
			rootCommand.Add(uploadCommand);

			var downloadCommand = new Command("download-files", "Download files from the MTP device.");
			downloadCommand.Aliases.Add("d");
			downloadCommand.Add(deviceNameOption);
			downloadCommand.Add(sourceFolderOption);
			downloadCommand.Add(targetFolderOption);
			downloadCommand.Add(skipExistingFilesOption);
			downloadCommand.Add(copyRecursiveOption);
			downloadCommand.Add(moveOption);
			downloadCommand.Add(filterSubfoldersOption);
			downloadCommand.Add(filterFilesOption);
			rootCommand.Add(downloadCommand);

			// set handlers
			listDevicesCommand.SetAction(_ => ListDevices());
			listFilesCommand.SetAction(parseResult =>
			{
				var deviceName = parseResult.GetRequiredValue(deviceNameOption);
				var sourceFolder = parseResult.GetRequiredValue(sourceFolderOption);
				var filterFilePattern = parseResult.GetValue(filterFilesOption);
				var fullPath = parseResult.GetValue(fullPathOption);
				ListFiles(deviceName, sourceFolder, filterFilePattern, fullPath);
			});

			uploadCommand.SetAction(parseResult =>
			{
				var deviceName = parseResult.GetRequiredValue(deviceNameOption);
				var sourceFolder = parseResult.GetRequiredValue(sourceFolderOption);
				var targetFolder = parseResult.GetRequiredValue(targetFolderOption);
				var skipExisting = parseResult.GetValue(skipExistingFilesOption);
				var recursive = parseResult.GetValue(copyRecursiveOption);
				var move = parseResult.GetValue(moveOption);
				var filterSubfolderPattern = parseResult.GetValue(filterSubfoldersOption);
				var filterFilePattern = parseResult.GetValue(filterFilesOption);
				CopyFiles("upload", deviceName, sourceFolder, targetFolder, skipExisting, recursive, filterSubfolderPattern, filterFilePattern, move);
			});

			downloadCommand.SetAction(parseResult =>
			{
				var deviceName = parseResult.GetRequiredValue(deviceNameOption);
				var sourceFolder = parseResult.GetRequiredValue(sourceFolderOption);
				var targetFolder = parseResult.GetRequiredValue(targetFolderOption);
				var skipExisting = parseResult.GetValue(skipExistingFilesOption);
				var recursive = parseResult.GetValue(copyRecursiveOption);
				var move = parseResult.GetValue(moveOption);
				var filterSubfolderPattern = parseResult.GetValue(filterSubfoldersOption);
				var filterFilePattern = parseResult.GetValue(filterFilesOption);
				CopyFiles("download", deviceName, sourceFolder, targetFolder, skipExisting, recursive, filterSubfolderPattern, filterFilePattern, move);
			});

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

		private static void ListFiles(string deviceName, string sourceFolder, string? filterFilePattern, bool fullPath)
		{
			using var device = GetDeviceByName(deviceName);

			string[] entries;
			try
			{
				entries = device.GetFiles(sourceFolder);
			}
			catch (DirectoryNotFoundException ex)
			{
				Console.WriteLine($"[{device.FriendlyName}] {ex.Message}");
				Environment.Exit(1);
				return;
			}

			Regex? filterFileRegex = null;
			if (!string.IsNullOrEmpty(filterFilePattern))
				filterFileRegex = new Regex(filterFilePattern);

			var orderedEntries = entries
				.Select(p => new
				{
					Raw = p,
					FileName = Path.GetFileName(p),
				})
				.Where(x => filterFileRegex == null || filterFileRegex.IsMatch(x.FileName))
				.Select(x => new
				{
					x.Raw,
					Display = fullPath ? x.Raw : x.FileName,
				})
				.OrderBy(x => x.Display, StringComparer.InvariantCultureIgnoreCase)
				.ThenBy(x => x.Display, StringComparer.InvariantCulture)
				.ThenBy(x => x.Raw, StringComparer.InvariantCultureIgnoreCase);

			foreach (var entry in orderedEntries)
				Console.WriteLine(entry.Display);
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

		private static string PrintVersion()
		{
			return Version;
		}
	}
}
