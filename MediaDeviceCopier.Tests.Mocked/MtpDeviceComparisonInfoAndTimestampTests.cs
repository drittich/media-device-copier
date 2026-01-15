using MediaDeviceCopier;
using MediaDevices;
using Xunit;

namespace MediaDeviceCopier.Tests.Mocked;

public class MtpDeviceComparisonInfoAndTimestampTests
{
	private static (MtpDevice device, IMediaDevice mock) CreateConnected(IMediaDevice mock)
	{
		mock.Connect();
		var device = new MtpDevice(mock);
		device.Connect();
		return (device, mock);
	}

	[Fact]
	public void Download_WhenSkipExistingAndLengthMatchesButDateDiffers_CopiesBecauseMismatch()
	{
		// Arrange
		var mock = new MockMediaDevice();
		var (device, _) = CreateConnected(mock);

		mock.AddFolder("/device");
		var sourcePath = "/device/same_size_date_mismatch.bin";
		var deviceBytes = new byte[] { 1, 2, 3, 4 };

		using var tempDir = new TempDirectory("MtpDeviceComparisonInfoAndTimestampTests_DownloadMismatchByDate_");
		var targetPath = Path.Combine(tempDir.Path, "same_size_date_mismatch.bin");

		// Pre-create local target with same length but different timestamp and different content
		var localBytes = new byte[] { 9, 9, 9, 9 };
		File.WriteAllBytes(targetPath, localBytes);

		var localTimestampRequested = new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Local);
		File.SetLastWriteTime(targetPath, localTimestampRequested);
		var localTimestampActual = File.GetLastWriteTime(targetPath);

		// Device file has same length but a different modified date, so download should NOT skip.
		var deviceTimestamp = localTimestampActual.AddMinutes(10);
		mock.AddFile(sourcePath, deviceBytes, deviceTimestamp);

		// Act
		var result = device.CopyFile(FileCopyMode.Download, sourcePath, targetPath, skipExisting: true, isMove: false);

		// Assert
		Assert.Equal(FileCopyStatus.CopiedBecauseDateOrSizeMismatch, result.CopyStatus);
		Assert.Equal((ulong)deviceBytes.Length, result.Length);
		Assert.Equal(deviceBytes, File.ReadAllBytes(targetPath));
	}

	[Fact]
	public void Upload_WhenSkipExistingAndLengthMatchesButDateDiffers_SkipsBecauseUploadComparesLengthOnly()
	{
		// Arrange
		var mock = new CountingMediaDevice();
		var (device, _) = CreateConnected(mock);

		using var tempDir = new TempDirectory("MtpDeviceComparisonInfoAndTimestampTests_UploadLengthOnly_");
		var sourcePath = Path.Combine(tempDir.Path, "upload.bin");
		var localBytes = new byte[] { 2, 2, 2 };
		File.WriteAllBytes(sourcePath, localBytes);

		var targetDevicePath = "/up/upload.bin";
		var deviceBytes = new byte[] { 1, 1, 1 }; // same length as local
		mock.AddDeviceFile(targetDevicePath, deviceBytes, modifiedDate: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local));

		// Act
		var result = device.CopyFile(FileCopyMode.Upload, sourcePath, targetDevicePath, skipExisting: true, isMove: false);

		// Assert
		Assert.Equal(FileCopyStatus.SkippedBecauseAlreadyExists, result.CopyStatus);
		Assert.Equal((ulong)localBytes.Length, result.Length);
		Assert.Equal(0, mock.UploadCallCount);
		Assert.Equal(deviceBytes, mock.GetDeviceBytes(targetDevicePath));
	}

	[Fact]
	public void Download_WhenDeviceComparisonInfoUnavailable_FallsBackToLocalFileInfoForLength()
	{
		// Arrange
		var mock = new ComparisonInfoUnavailableMediaDevice();
		var (device, _) = CreateConnected(mock);

		var sourcePath = "/device/no_cmp.bin";
		var deviceBytes = new byte[] { 5, 6, 7, 8, 9 };
		mock.AddDeviceFile(sourcePath, deviceBytes);

		using var tempDir = new TempDirectory("MtpDeviceComparisonInfoAndTimestampTests_DownloadFallback_");
		var targetPath = Path.Combine(tempDir.Path, "no_cmp.bin");

		// Act
		var result = device.CopyFile(FileCopyMode.Download, sourcePath, targetPath, skipExisting: false, isMove: false);

		// Assert
		Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
		Assert.True(File.Exists(targetPath));
		Assert.Equal(deviceBytes, File.ReadAllBytes(targetPath));
		Assert.Equal((ulong)new FileInfo(targetPath).Length, result.Length);
	}

	[Fact]
	public void Upload_WhenDeviceComparisonInfoUnavailable_FallsBackToLocalSourceFileInfoForLength()
	{
		// Arrange
		var mock = new ComparisonInfoUnavailableMediaDevice();
		var (device, _) = CreateConnected(mock);

		using var tempDir = new TempDirectory("MtpDeviceComparisonInfoAndTimestampTests_UploadFallback_");
		var sourcePath = Path.Combine(tempDir.Path, "no_cmp_upload.bin");
		var localBytes = new byte[] { 10, 11, 12, 13 };
		File.WriteAllBytes(sourcePath, localBytes);

		var targetDevicePath = "/up/no_cmp_upload.bin";

		// Act
		var result = device.CopyFile(FileCopyMode.Upload, sourcePath, targetDevicePath, skipExisting: false, isMove: false);

		// Assert
		Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
		Assert.Equal((ulong)localBytes.Length, result.Length);
		Assert.Equal(localBytes, mock.GetDeviceBytes(targetDevicePath));
	}

	[Fact]
	public void Download_WhenComparisonInfoHasValidDate_SetsTargetLastWriteTime()
	{
		// Arrange
		var mock = new MockMediaDevice();
		var (device, _) = CreateConnected(mock);

		mock.AddFolder("/device");
		var sourcePath = "/device/valid_date.bin";
		var bytes = new byte[] { 1, 2, 3 };
		var desiredTimestamp = new DateTime(2024, 05, 17, 12, 0, 0, DateTimeKind.Local);
		mock.AddFile(sourcePath, bytes, desiredTimestamp);

		using var tempDir = new TempDirectory("MtpDeviceComparisonInfoAndTimestampTests_ValidDateSets_");
		var targetPath = Path.Combine(tempDir.Path, "valid_date.bin");

		// Act
		var result = device.CopyFile(FileCopyMode.Download, sourcePath, targetPath, skipExisting: false, isMove: false);

		// Assert
		Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
		Assert.True(File.Exists(targetPath));

		var actual = File.GetLastWriteTime(targetPath);
		// Allow small tolerance for filesystem precision/rounding.
		Assert.True(Math.Abs((actual - desiredTimestamp).TotalSeconds) <= 2,
			$"Expected last write time within 2s. Desired={desiredTimestamp:o}, Actual={actual:o}");
	}

	[Fact]
	public void Download_WhenComparisonInfoHasInvalidPre1601Date_DoesNotSetTargetLastWriteTimeToInvalidDate()
	{
		// Arrange
		var mock = new MockMediaDevice();
		var (device, _) = CreateConnected(mock);

		mock.AddFolder("/device");
		var sourcePath = "/device/invalid_date.bin";
		var bytes = new byte[] { 1, 2, 3, 4, 5, 6 };
		var invalidTimestamp = new DateTime(1500, 01, 01, 0, 0, 0, DateTimeKind.Local);
		mock.AddFile(sourcePath, bytes, invalidTimestamp);

		using var tempDir = new TempDirectory("MtpDeviceComparisonInfoAndTimestampTests_InvalidDateNotSet_");
		var targetPath = Path.Combine(tempDir.Path, "invalid_date.bin");

		// Act
		var result = device.CopyFile(FileCopyMode.Download, sourcePath, targetPath, skipExisting: false, isMove: false);

		// Assert
		Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
		Assert.True(File.Exists(targetPath));

		var actual = File.GetLastWriteTime(targetPath);
		Assert.NotEqual(invalidTimestamp, actual);
		Assert.True(actual.Year >= 1601, $"Expected filesystem timestamp year >= 1601, actual was {actual:o}");
		Assert.True(Math.Abs((actual - DateTime.Now).TotalMinutes) <= 2,
			$"Expected timestamp to be near current time (not forced to invalid). Actual={actual:o}");
	}

	private sealed class TempDirectory : IDisposable
	{
		public string Path { get; }

		public TempDirectory(string prefix)
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path);
		}

		public void Dispose()
		{
			try
			{
				if (Directory.Exists(Path))
				{
					Directory.Delete(Path, recursive: true);
				}
			}
			catch
			{
				// ignore cleanup failures
			}
		}
	}

	/// <summary>
	/// IMediaDevice test double that supports InternalGetComparisonInfo and exposes bytes for assertions.
	/// </summary>
	private sealed class CountingMediaDevice : IMediaDevice
	{
		private readonly Dictionary<string, (byte[] Bytes, DateTime ModifiedDate)> _files = new(StringComparer.Ordinal);

		public int UploadCallCount { get; private set; }

		public bool IsConnected { get; private set; }
		public string FriendlyName => nameof(CountingMediaDevice);
		public void Connect() => IsConnected = true;
		public void Dispose() { }

		public bool FileExists(string path) => _files.ContainsKey(path);
		public bool DirectoryExists(string folder) => true;
		public void CreateDirectory(string folder) { }
		public string[] GetDirectories(string folder) => Array.Empty<string>();
		public string[] GetFiles(string folder) => Array.Empty<string>();
		public void DeleteFile(string path) => _files.Remove(path);

		public MediaFileInfo GetFileInfo(string path) => throw new NotImplementedException();

		public void DownloadFile(string sourceFilePath, string targetFilePath)
		{
			File.WriteAllBytes(targetFilePath, _files[sourceFilePath].Bytes);
		}

		public void UploadFile(string sourceFilePath, string targetFilePath)
		{
			UploadCallCount++;
			_files[targetFilePath] = (File.ReadAllBytes(sourceFilePath), DateTime.Now);
		}

		public void AddDeviceFile(string path, byte[] bytes, DateTime modifiedDate)
		{
			_files[path] = (bytes, modifiedDate);
		}

		public byte[] GetDeviceBytes(string path) => _files[path].Bytes;

		public FileComparisonInfo InternalGetComparisonInfo(string path)
		{
			var (bytes, modifiedDate) = _files[path];
			return new FileComparisonInfo { Length = (ulong)bytes.Length, ModifiedDate = modifiedDate };
		}
	}

	/// <summary>
	/// IMediaDevice test double that intentionally cannot provide comparison info:
	/// - does NOT implement InternalGetComparisonInfo
	/// - GetFileInfo always throws
	/// This forces MtpDevice.TryGetComparisonInfoFromDevice to return (sentinel,false).
	/// </summary>
	private sealed class ComparisonInfoUnavailableMediaDevice : IMediaDevice
	{
		private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

		public bool IsConnected { get; private set; }
		public string FriendlyName => nameof(ComparisonInfoUnavailableMediaDevice);
		public void Connect() => IsConnected = true;
		public void Dispose() { }

		public bool FileExists(string path) => _files.ContainsKey(path);
		public bool DirectoryExists(string folder) => true;
		public void CreateDirectory(string folder) { }
		public string[] GetDirectories(string folder) => Array.Empty<string>();
		public string[] GetFiles(string folder) => Array.Empty<string>();
		public void DeleteFile(string path) => _files.Remove(path);

		public MediaFileInfo GetFileInfo(string path) => throw new NotSupportedException("GetFileInfo intentionally unavailable for this test");

		public void DownloadFile(string sourceFilePath, string targetFilePath)
		{
			File.WriteAllBytes(targetFilePath, _files[sourceFilePath]);
		}

		public void UploadFile(string sourceFilePath, string targetFilePath)
		{
			_files[targetFilePath] = File.ReadAllBytes(sourceFilePath);
		}

		public void AddDeviceFile(string path, byte[] bytes)
		{
			_files[path] = bytes;
		}

		public byte[] GetDeviceBytes(string path) => _files[path];
	}
}
