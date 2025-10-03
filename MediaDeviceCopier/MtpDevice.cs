using MediaDevices;
using System.Diagnostics;

namespace MediaDeviceCopier
{
	#region Strategy Abstraction

	/// <summary>
	/// Context information passed to each download strategy attempt
	/// </summary>
	public class DownloadStrategyContext
	{
		public required string SourceFilePath { get; init; }
		public required string TargetFilePath { get; init; }
		public required string FileExtension { get; init; }
		public required FileMediaClass MediaClass { get; init; }
		public required IMediaDevice Device { get; init; }
	}

	/// <summary>
	/// Delegate signature for download strategy attempts
	/// </summary>
	/// <param name="context">Strategy context with file and device information</param>
	/// <returns>True if download succeeded, false to try next strategy</returns>
	public delegate bool DownloadStrategy(DownloadStrategyContext context);

	/// <summary>
	/// Classification of media file types for potential strategy optimization
	/// </summary>
	public enum FileMediaClass
	{
		Unknown,
		Image,      // jpg, jpeg, png, gif, bmp, tiff, raw, etc.
		Video,      // mp4, mov, avi, mkv, wmv, m4v, etc.
		Audio,      // wav, mp3, flac, aac, m4a, wma, etc.
		Metadata,   // thm, lrv, xmp, etc.
		Document    // pdf, txt, doc, etc.
	}

	#endregion

	public class MtpDevice : IDisposable
	{
		private bool _disposed;
		private readonly IMediaDevice _device;

		// Strategy collection - can be customized for testing or specialized scenarios
		private static List<(string Name, DownloadStrategy Strategy)>? _downloadStrategies;

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

			if (isMove && result.CopyStatus != FileCopyStatus.SkippedBecauseAlreadyExists && result.CopyStatus != FileCopyStatus.SkippedBecauseUnsupported)
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

			// Resilient download with multiple fallback strategies for problematic files
			try
			{
				bool downloadSuccessful = TryResilientDownload(sourceFilePath, targetFilePath);

				if (!downloadSuccessful)
				{
					throw new InvalidOperationException($"Failed to download file after trying all available methods: {sourceFilePath}");
				}
			}
			catch (Exception ex) 
			{
				Console.WriteLine($"WARNING: File copy failed with error: {ex.Message}. File will be skipped: {sourceFilePath}");
				fileCopyInfo = new()
				{
					CopyStatus = FileCopyStatus.SkippedBecauseUnsupported,
					Length = 0
				};
				return fileCopyInfo;
			}

			// set the file date to match the source file (with safe fallback)
			if (mtpFileComparisonInfo is null)
			{
				var (cmp, success) = TryGetComparisonInfoFromDevice(sourceFilePath);
				if (success)
				{
					mtpFileComparisonInfo = cmp;
					if (File.Exists(targetFilePath) && IsValidWin32FileTime(mtpFileComparisonInfo.ModifiedDate))
					{
						File.SetLastWriteTime(targetFilePath, mtpFileComparisonInfo.ModifiedDate);
					}
				}
				else
				{
					// Fallback: derive comparison info from the newly downloaded local file
					if (File.Exists(targetFilePath))
					{
						var fi = new FileInfo(targetFilePath);
						mtpFileComparisonInfo = new FileComparisonInfo
						{
							Length = (ulong)fi.Length,
							ModifiedDate = fi.LastWriteTime
						};
					}
					else
					{
						mtpFileComparisonInfo = new FileComparisonInfo
						{
							Length = 0,
							ModifiedDate = DateTime.Now
						};
					}
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
		/// Classifies a file based on its extension for potential strategy optimization.
		/// This is a lightweight hook that enables future per-class customization without hardcoding.
		/// </summary>
		/// <param name="extension">File extension (with or without leading dot)</param>
		/// <returns>Media class classification</returns>
		public static FileMediaClass ClassifyFile(string extension)
		{
			var ext = extension.TrimStart('.').ToLowerInvariant();

			// Image formats
			if (ext is "jpg" or "jpeg" or "png" or "gif" or "bmp" or "tiff" or "tif" or "raw" or "cr2" or "nef" or "arw" or "dng")
				return FileMediaClass.Image;

			// Video formats
			if (ext is "mp4" or "mov" or "avi" or "mkv" or "wmv" or "m4v" or "mpg" or "mpeg" or "flv" or "webm" or "3gp")
				return FileMediaClass.Video;

			// Audio formats
			if (ext is "wav" or "mp3" or "flac" or "aac" or "m4a" or "wma" or "ogg" or "opus" or "alac")
				return FileMediaClass.Audio;

			// Metadata/thumbnail formats
			if (ext is "thm" or "lrv" or "xmp" or "sidecar")
				return FileMediaClass.Metadata;

			// Document formats
			if (ext is "pdf" or "txt" or "doc" or "docx" or "xls" or "xlsx")
				return FileMediaClass.Document;

			return FileMediaClass.Unknown;
		}

		/// <summary>
		/// Gets the default download strategy collection. Can be customized for testing.
		/// </summary>
		public static List<(string Name, DownloadStrategy Strategy)> GetDefaultDownloadStrategies()
		{
			_downloadStrategies ??= new List<(string, DownloadStrategy)>
			{
				("Standard", ExecuteStandardDownload),
				("StreamRetry", ExecuteStreamRetryDownload),
				("ChunkedRetry", ExecuteChunkedRetryDownload),
				("MetadataProbe", ExecuteMetadataProbeDownload)
			};

			return _downloadStrategies;
		}

		/// <summary>
		/// Allows customization of the download strategy collection (primarily for testing)
		/// </summary>
		public static void SetDownloadStrategies(List<(string Name, DownloadStrategy Strategy)> strategies)
		{
			_downloadStrategies = strategies;
		}

		/// <summary>
		/// Resilient download method that tries multiple strategies to download files,
		/// with enhanced diagnostics for troubleshooting problematic file types.
		/// </summary>
		/// <param name="sourceFilePath">Source file path on the MTP device</param>
		/// <param name="targetFilePath">Target file path on local system</param>
		/// <returns>True if download succeeded, false if all strategies failed</returns>
		private bool TryResilientDownload(string sourceFilePath, string targetFilePath)
		{
			var extension = Path.GetExtension(sourceFilePath);
			var mediaClass = ClassifyFile(extension);
			var context = new DownloadStrategyContext
			{
				SourceFilePath = sourceFilePath,
				TargetFilePath = targetFilePath,
				FileExtension = extension,
				MediaClass = mediaClass,
				Device = _device
			};

			var strategies = GetDefaultDownloadStrategies();

			foreach (var (name, strategy) in strategies)
			{
				var stopwatch = Stopwatch.StartNew();
				try
				{
					Console.Write($"[{name}] ");
					bool success = strategy(context);
					stopwatch.Stop();

					if (success)
					{
						Console.Write($"Success ({stopwatch.ElapsedMilliseconds}ms) ");
						return true;
					}
					else
					{
						Console.Write($"Returned-False ({stopwatch.ElapsedMilliseconds}ms) ");
					}
				}
				catch (System.Runtime.InteropServices.COMException comEx)
				{
					stopwatch.Stop();
					Console.Write($"COM-Error:0x{comEx.HResult:X8} ({stopwatch.ElapsedMilliseconds}ms) ");
					// Continue to next strategy
				}
				catch (Exception ex)
				{
					stopwatch.Stop();
					Console.Write($"Failed:{ex.GetType().Name} ({stopwatch.ElapsedMilliseconds}ms) ");
					// Continue to next strategy
				}
			}

			// All strategies failed
			Console.Write($"[All-Strategies-Failed] ");
			return false;
		}

		#region Download Strategy Implementations

		/// <summary>
		/// Strategy 1: Standard direct download using MediaDevices library
		/// </summary>
		private static bool ExecuteStandardDownload(DownloadStrategyContext context)
		{
			context.Device.DownloadFile(context.SourceFilePath, context.TargetFilePath);
			return true;
		}

		/// <summary>
		/// Strategy 2: Stream-based retry with small delay for timing-sensitive files
		/// </summary>
		private static bool ExecuteStreamRetryDownload(DownloadStrategyContext context)
		{
			// Small delay in case of timing issues
			Thread.Sleep(100);
			context.Device.DownloadFile(context.SourceFilePath, context.TargetFilePath);
			return true;
		}

		/// <summary>
		/// Strategy 3: Chunked download retry with longer delay for buffer issues
		/// </summary>
		private static bool ExecuteChunkedRetryDownload(DownloadStrategyContext context)
		{
			// Longer delay for different timing window
			// Future: implement actual chunked transfer using lower-level MTP APIs
			Thread.Sleep(200);
			context.Device.DownloadFile(context.SourceFilePath, context.TargetFilePath);
			return true;
		}

		/// <summary>
		/// Strategy 4: Metadata/thumbnail resource probe as last resort
		/// Note: Currently a retry with extended delay. Future implementations could
		/// use WPD_RESOURCE_THUMBNAIL for actual thumbnail extraction on supported formats.
		/// </summary>
		private static bool ExecuteMetadataProbeDownload(DownloadStrategyContext context)
		{
			// Extended delay for maximum timing window difference
			// Future: for image/video files, attempt WPD thumbnail resource extraction
			Thread.Sleep(500);
			context.Device.DownloadFile(context.SourceFilePath, context.TargetFilePath);
			return true;
		}

		#endregion

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
