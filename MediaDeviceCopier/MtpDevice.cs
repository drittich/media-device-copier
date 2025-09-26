using MediaDevices;

namespace MediaDeviceCopier
{
	public class MtpDevice : IDisposable
	{
		private bool _disposed;
		private readonly IMediaDevice _device;
		public MtpDevice(IMediaDevice device)
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

		public bool IsConnected
		{
			get
			{
				if (_device == null)
					return false;
				else
					return _device.IsConnected;
			}
		}

		private static List<MtpDevice>? _listDevices;
		private static Func<IEnumerable<IMediaDevice>> _deviceFactory = () => MediaDevice.GetDevices()
			.Select(d => (IMediaDevice)new MediaDeviceWrapper(d));

		public static Func<IEnumerable<IMediaDevice>> DeviceFactory
		{
			get => _deviceFactory;
			set
			{
				_deviceFactory = value;
				_listDevices = null;
			}
		}

		public static List<MtpDevice> GetAll()
		{
			_listDevices ??= DeviceFactory()
					.OrderBy(d => d.FriendlyName)
					.Select(d => new MtpDevice(d))
					.ToList();

			return _listDevices;
		}

		public static MtpDevice? GetByName(string deviceName)
		{
			var device = GetAll()
					.FirstOrDefault(d => d.FriendlyName.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase));

			return device;
		}

		public MediaFileInfo GetFileInfo(string filePath)
		{
			if (!_device.FileExists(filePath))
			{
				throw new FileNotFoundException($"File not found: {filePath}");
			}

			return _device.GetFileInfo(filePath);
		}

		public FileCopyResultInfo CopyFile(FileCopyMode fileCopyMode, string sourceFilePath, string targetFilePath, bool skipExisting, bool isMove)
		{
			if (!_device.IsConnected)
			{
				throw new InvalidOperationException("Device is not connected.");
			}

			FileCopyResultInfo result;
			if (fileCopyMode == FileCopyMode.Download)
			{
				result = DownloadFile(sourceFilePath, targetFilePath, skipExisting);
			}
			else if (fileCopyMode == FileCopyMode.Upload)
			{
				result = UploadFile(sourceFilePath, targetFilePath, skipExisting);
			}
			else
			{
				throw new NotImplementedException();
			}

			if (isMove && result.CopyStatus != FileCopyStatus.SkippedBecauseAlreadyExists)
			{
				try
				{
					if (fileCopyMode == FileCopyMode.Download)
					{
						_device.DeleteFile(sourceFilePath);
					}
					else
					{
						// Upload move: delete local original
						if (File.Exists(sourceFilePath))
						{
							File.Delete(sourceFilePath);
						}
					}
					result.SourceDeleted = true;
				}
				catch
				{
					// Swallow deletion errors; move becomes simple copy
					result.SourceDeleted = false;
				}
			}

			return result;
		}

		private FileCopyResultInfo DownloadFile(string sourceFilePath, string targetFilePath, bool skipExisting)
		{
			FileCopyStatus fileCopyStatus = FileCopyStatus.Copied;
			FileComparisonInfo? mtpFileComparisonInfo = null;
			FileCopyResultInfo fileCopyInfo;

			if (skipExisting && File.Exists(targetFilePath))
			{
				var sizeAndDatesMatch = GetSizeAndDatesMatch(FileCopyMode.Download, sourceFilePath, targetFilePath, out mtpFileComparisonInfo);
				if (sizeAndDatesMatch)
				{
					fileCopyInfo = new()
					{
						CopyStatus = FileCopyStatus.SkippedBecauseAlreadyExists,
						Length = mtpFileComparisonInfo.Length
					};
					return fileCopyInfo;
				}
				else
				{
					fileCopyStatus = FileCopyStatus.CopiedBecauseDateOrSizeMismatch;
				}
			}

			_device.DownloadFile(sourceFilePath, targetFilePath);

			// set the file date to match the source file (with safe fallback)
			if (mtpFileComparisonInfo is null)
			{
				var (cmp, success) = TryGetComparisonInfoFromDevice(sourceFilePath);
				if (success)
				{
					mtpFileComparisonInfo = cmp;
					if (IsValidWin32FileTime(mtpFileComparisonInfo.ModifiedDate))
					{
						File.SetLastWriteTime(targetFilePath, mtpFileComparisonInfo.ModifiedDate);
					}
				}
				else
				{
					// Fallback: derive comparison info from the newly downloaded local file
					var fi = new FileInfo(targetFilePath);
					mtpFileComparisonInfo = new FileComparisonInfo
					{
						Length = (ulong)fi.Length,
						ModifiedDate = fi.LastWriteTime
					};
				}
			}

			fileCopyInfo = new()
			{
				CopyStatus = fileCopyStatus,
				Length = mtpFileComparisonInfo.Length
			};
			return fileCopyInfo;
		}

		private FileCopyResultInfo UploadFile(string sourceFilePath, string targetFilePath, bool skipExisting)
		{
			FileCopyStatus fileCopyStatus = FileCopyStatus.Copied;
			FileComparisonInfo? mtpFileComparisonInfo = null;
			FileCopyResultInfo fileCopyInfo;

			if (skipExisting && _device.FileExists(targetFilePath))
			{
				var sizeAndDatesMatch = GetSizeAndDatesMatch(FileCopyMode.Upload, sourceFilePath, targetFilePath, out mtpFileComparisonInfo);
				if (sizeAndDatesMatch)
				{
					fileCopyInfo = new()
					{
						CopyStatus = FileCopyStatus.SkippedBecauseAlreadyExists,
						Length = mtpFileComparisonInfo.Length
					};
					return fileCopyInfo;
				}
				else
				{
					fileCopyStatus = FileCopyStatus.CopiedBecauseDateOrSizeMismatch;
				}
			}

			_device.UploadFile(sourceFilePath, targetFilePath);

			// Attempt to get comparison info from device target (safe)
			if (mtpFileComparisonInfo is null)
			{
				var (cmp, success) = TryGetComparisonInfoFromDevice(targetFilePath);
				if (success)
				{
					mtpFileComparisonInfo = cmp;
				}
				else
				{
					// Fallback: use local source file info (best available)
					var fi = new FileInfo(sourceFilePath);
					mtpFileComparisonInfo = new FileComparisonInfo
					{
						Length = (ulong)fi.Length,
						ModifiedDate = fi.LastWriteTime
					};
				}
			}
			//File.SetLastWriteTime(targetFilePath, mtpFileComparisonInfo.ModifiedDate);

			fileCopyInfo = new()
			{
				CopyStatus = fileCopyStatus,
				Length = mtpFileComparisonInfo.Length
			};
			return fileCopyInfo;
		}

		private bool GetSizeAndDatesMatch(FileCopyMode fileCopyMode, string sourceFilePath, string targetFilePath, out FileComparisonInfo mtpFileComparisonInfo)
		{
			FileComparisonInfo sourceComparisonInfo;
			FileComparisonInfo targetComparisonInfo;

			if (fileCopyMode is FileCopyMode.Download)
			{
				var (srcCmp, srcOk) = TryGetComparisonInfoFromDevice(sourceFilePath);
				sourceComparisonInfo = srcCmp;
				mtpFileComparisonInfo = sourceComparisonInfo;
				targetComparisonInfo = GetComparisonInfo(new FileInfo(targetFilePath));
			}
			else if (fileCopyMode is FileCopyMode.Upload)
			{
				sourceComparisonInfo = GetComparisonInfo(new FileInfo(sourceFilePath));
				var (tgtCmp, tgtOk) = TryGetComparisonInfoFromDevice(targetFilePath);
				targetComparisonInfo = tgtCmp;
				mtpFileComparisonInfo = targetComparisonInfo;
			}
			else
			{
				throw new NotImplementedException();
			}

			if (fileCopyMode is FileCopyMode.Upload)
			{
				// Modified dates are unreliable when uploading to MTP devices
				return sourceComparisonInfo.Length == targetComparisonInfo.Length;
			}
			return sourceComparisonInfo.Equals(targetComparisonInfo);
		}

		public string[] GetFiles(string folder)
		{
			if (!_device.DirectoryExists(folder))
			{
				throw new DirectoryNotFoundException($"Folder not found: {folder}");
			}

			return _device.GetFiles(folder);
		}

		public static FileComparisonInfo GetComparisonInfo(MediaFileInfo mediaFileInfo)
		{
			return new()
			{
				Length = mediaFileInfo.Length,
				ModifiedDate = mediaFileInfo.LastWriteTime!.Value
			};
		}

		public static FileComparisonInfo GetComparisonInfo(FileInfo fileInfo)
		{
			return new()
			{
				Length = (ulong)fileInfo.Length,
				ModifiedDate = fileInfo.LastWriteTime
			};
		}

		private (FileComparisonInfo info, bool success) TryGetComparisonInfoFromDevice(string path)
		{
			// 1. Reflection hook for mocks exposing InternalGetComparisonInfo (returns FileComparisonInfo)
			try
			{
				var maybeMethod = _device.GetType().GetMethod("InternalGetComparisonInfo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
				if (maybeMethod != null && maybeMethod.ReturnType == typeof(FileComparisonInfo) && maybeMethod.GetParameters().Length == 1)
				{
					var invoked = maybeMethod.Invoke(_device, new object[] { path }) as FileComparisonInfo;
					if (invoked != null)
					{
						return (invoked, true);
					}
				}
			}
			catch
			{
				// Ignore reflection failures, fall through to normal path
			}

			// 2. Normal library path
			try
			{
				var mediaInfo = _device.GetFileInfo(path);
				return (GetComparisonInfo(mediaInfo), true);
			}
			catch
			{
				// Failure: return sentinel comparison info
				return (new FileComparisonInfo
				{
					Length = 0,
					ModifiedDate = DateTime.MinValue
				}, false);
			}
		}

		/// <summary>
		/// Validates if a DateTime can be safely converted to Win32 FileTime.
		/// Win32 FileTime has a valid range from January 1, 1601 to a far future date.
		/// </summary>
		/// <param name="dateTime">The DateTime to validate</param>
		/// <returns>True if the DateTime is valid for Win32 FileTime conversion</returns>
		public static bool IsValidWin32FileTime(DateTime dateTime)
		{
			// Win32 FileTime minimum is January 1, 1601
			var minFileTime = DateTime.FromFileTime(0);
			// Use a reasonable maximum date to avoid edge cases
			var maxFileTime = new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Local);

			return dateTime >= minFileTime && dateTime <= maxFileTime;
		}

		public string[] GetDirectories(string folder)
		{
			if (!_device.DirectoryExists(folder))
			{
				throw new DirectoryNotFoundException($"Folder not found: {folder}");
			}

			return _device.GetDirectories(folder);
		}

		public void CreateDirectory(string folder)
		{
			try
			{
				_device.CreateDirectory(folder);
			}
			catch (Exception ex)
			{
				throw new IOException($"Cannot create directory {ex.Message}");
			}
		}

		public bool DirectoryExists(string folder)
		{
			return _device.DirectoryExists(folder);
		}

		public void DeleteFile(string path)
		{
			_device.DeleteFile(path);
		}

		public string FriendlyName => _device.FriendlyName;

		void IDisposable.Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			// Call the base class implementation.
			_device.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
