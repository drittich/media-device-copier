using MediaDevices;

namespace MediaDeviceCopier
{
	public class MtpDevice
	{
		private MediaDevice _device;
		public MtpDevice(MediaDevice device)
		{
			_device = device;
		}

		public void Connect()
		{
			if (_device.IsConnected)
			{
				return;
			}

			_device.Connect();
		}

		private static List<MediaDevice>? _listDevices;
		public static List<MediaDevice> GetAll()
		{
			if (_listDevices is null)
			{

				_listDevices = MediaDevice.GetDevices()
					.OrderBy(d => d.FriendlyName)
					.ToList();
			}

			return _listDevices;
		}

		public static MtpDevice? GetByName(string deviceName)
		{
			var device = GetAll()
				.FirstOrDefault(d => d.FriendlyName.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase));

			if (device is null)
			{
				return null;
			}

			return new MtpDevice(device);
		}

		public MediaFileInfo GetFileInfo(string filePath)
		{
			if (!_device.FileExists(filePath))
			{
				throw new FileNotFoundException($"File not found: {filePath}");
			}

			return _device.GetFileInfo(filePath);
		}

		public FileCopyStatus CopyFile(FileCopyMode fileCopyMode, string sourceFilePath, string targetFilePath, bool skipExisting)
		{
			if (!_device.IsConnected)
			{
				throw new InvalidOperationException("Device is not connected.");
			}

			if (fileCopyMode == FileCopyMode.Download)
			{
				return DownloadFile(sourceFilePath, targetFilePath, skipExisting);
			}
			else if (fileCopyMode == FileCopyMode.Upload)
			{
				return UploadFile(sourceFilePath, targetFilePath, skipExisting);
			}
			else
			{
				throw new NotImplementedException();
			}
		}

		private FileCopyStatus UploadFile(string sourceFilePath, string targetFilePath, bool skipExisting)
		{
			FileCopyStatus fileCopyStatus = FileCopyStatus.Copied;

			if (skipExisting && _device.FileExists(targetFilePath))
			{
				// TODO: check that file size and dates match and skip if they do
				var sizeAndDatesMatch = GetSizeAndDatesMatch(FileCopyMode.Upload, sourceFilePath, targetFilePath);
				if (sizeAndDatesMatch)
				{
					Console.WriteLine($"Skipping existing file: {targetFilePath}");
					return FileCopyStatus.SkippedBecauseAlreadyExists;
				}
				else
				{
					fileCopyStatus = FileCopyStatus.CopiedBecauseDateOrSizeMismatch;
					Console.WriteLine($"Overwriting existing file, date or size mismatch: {targetFilePath}");
				}
			}

			_device.UploadFile(sourceFilePath, targetFilePath);
			return fileCopyStatus;
		}

		private FileCopyStatus DownloadFile(string sourceFilePath, string targetFilePath, bool skipExisting)
		{
			FileCopyStatus fileCopyStatus = FileCopyStatus.Copied;

			if (skipExisting && File.Exists(targetFilePath))
			{
				// TODO: check that file size and dates match and skip if they do
				var sizeAndDatesMatch = GetSizeAndDatesMatch(FileCopyMode.Download, sourceFilePath, targetFilePath);
				if (sizeAndDatesMatch)
				{
					Console.WriteLine($"Skipping existing file: {targetFilePath}");
					return FileCopyStatus.SkippedBecauseAlreadyExists;
				}
				else
				{
					fileCopyStatus = FileCopyStatus.CopiedBecauseDateOrSizeMismatch;
					Console.WriteLine($"Overwriting existing file, date or size mismatch: {targetFilePath}");
				}
			}

			_device.DownloadFile(sourceFilePath, targetFilePath);
			return fileCopyStatus;
		}

		private bool GetSizeAndDatesMatch(FileCopyMode fileCopyMode, string sourceFilePath, string targetFilePath)
		{
			if (fileCopyMode is FileCopyMode.Download)
			{
				var sourceFileInfo = _device.GetFileInfo(sourceFilePath);
				var targetFileInfo = new FileInfo(targetFilePath);

				return sourceFileInfo.Length == (ulong)targetFileInfo.Length
					&& sourceFileInfo.LastWriteTime == targetFileInfo.LastWriteTimeUtc;
			}
			else if (fileCopyMode is FileCopyMode.Upload)
			{
				var sourceFileInfo = new FileInfo(sourceFilePath);
				var targetFileInfo = _device.GetFileInfo(targetFilePath);

				return (ulong)sourceFileInfo.Length == targetFileInfo.Length
					&& sourceFileInfo.LastWriteTimeUtc == targetFileInfo.LastWriteTime;
			}
			else
			{
				throw new NotImplementedException();
			}
		}

		public string[] GetFiles(string folder)
		{
			if (!_device.DirectoryExists(folder))
			{
				throw new DirectoryNotFoundException($"Folder not found: {folder}");
			}

			return _device.GetFiles(folder);
		}
	}
}
