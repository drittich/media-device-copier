using MediaDeviceCopier;
using MediaDevices;
using Xunit;

namespace MediaDeviceCopier.Tests.Mocked
{
	public class CopyFileEdgeCaseTests
	{
		private static (MtpDevice device, MockMediaDevice mock) CreateConnectedMock()
		{
			var mock = new MockMediaDevice();
			mock.Connect();
			var device = new MtpDevice(mock);
			device.Connect();
			return (device, mock);
		}

		[Fact]
		public void CopyFile_WhenDeviceNotConnected_ThrowsInvalidOperationException()
		{
			// Arrange
			var mock = new MockMediaDevice();
			var device = new MtpDevice(mock);

			// Act + Assert
			Assert.Throws<InvalidOperationException>(() =>
				device.CopyFile(FileCopyMode.Download, "/device/file.bin", "C:/does/not/matter.bin", skipExisting: false, isMove: false));
		}

		[Fact]
		public void CopyFile_WhenFileCopyModeIsUnknown_ThrowsNotImplementedException()
		{
			// Arrange
			var (device, _) = CreateConnectedMock();

			// Act + Assert
			Assert.Throws<NotImplementedException>(() =>
				device.CopyFile((FileCopyMode)123, "irrelevant", "irrelevant", skipExisting: false, isMove: false));
		}

		[Fact]
		public void MoveDownload_WhenSkipExistingAndMismatch_CopiesBecauseMismatch_AndDeletesDeviceSource()
		{
			// Arrange
			var (device, mock) = CreateConnectedMock();
			mock.AddFolder("/device");

			var sourcePath = "/device/mismatch.bin";
			mock.AddFile(sourcePath, new byte[] { 1, 2, 3, 4 });

			using var tempDir = new TempDirectory();
			var targetPath = Path.Combine(tempDir.Path, "mismatch.bin");

			// Pre-create a different-length file so size/date comparison fails
			File.WriteAllBytes(targetPath, new byte[] { 9 });

			// Act
			var result = device.CopyFile(FileCopyMode.Download, sourcePath, targetPath, skipExisting: true, isMove: true);

			// Assert
			Assert.Equal(FileCopyStatus.CopiedBecauseDateOrSizeMismatch, result.CopyStatus);
			Assert.True(File.Exists(targetPath));
			Assert.False(mock.FileExists(sourcePath));
			Assert.True(result.SourceDeleted);
		}

		[Fact]
		public void MoveUpload_WhenSkipExistingAndMismatch_CopiesBecauseMismatch_AndDeletesLocalSource()
		{
			// Arrange
			var (device, mock) = CreateConnectedMock();

			using var tempDir = new TempDirectory();
			var sourcePath = Path.Combine(tempDir.Path, "upload_mismatch.bin");
			File.WriteAllBytes(sourcePath, new byte[] { 10, 20, 30 }); // length 3

			var targetDevicePath = "/up/upload_mismatch.bin";
			mock.AddFile(targetDevicePath, new byte[] { 1, 2 }); // length 2 to force mismatch

			// Act
			var result = device.CopyFile(FileCopyMode.Upload, sourcePath, targetDevicePath, skipExisting: true, isMove: true);

			// Assert
			Assert.Equal(FileCopyStatus.CopiedBecauseDateOrSizeMismatch, result.CopyStatus);
			Assert.False(File.Exists(sourcePath));
			Assert.True(mock.FileExists(targetDevicePath));
			Assert.True(result.SourceDeleted);
		}

		[Fact]
		public void MoveUpload_WhenLocalSourceMissing_SourceDeletedIsTrue()
		{
			// Arrange
			var mock = new UploadIgnoresMissingSourceMock();
			mock.Connect();
			var device = new MtpDevice(mock);
			device.Connect();

			using var tempDir = new TempDirectory();
			var sourcePath = Path.Combine(tempDir.Path, "does_not_exist.bin");
			Assert.False(File.Exists(sourcePath));
			var targetDevicePath = "/up/does_not_exist.bin";

			// Act
			var result = device.CopyFile(FileCopyMode.Upload, sourcePath, targetDevicePath, skipExisting: false, isMove: true);

			// Assert
			Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
			Assert.False(File.Exists(sourcePath));
			Assert.True(result.SourceDeleted);
		}

		[Fact]
		public void MoveUpload_WhenLocalDeleteFails_CopySucceedsButSourceDeletedFalse()
		{
			// Arrange
			var (device, mock) = CreateConnectedMock();
			using var tempDir = new TempDirectory();
			var sourcePath = Path.Combine(tempDir.Path, "locked.bin");
			File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
			var targetDevicePath = "/up/locked.bin";

			FileStream? lockStream = null;
			try
			{
				// Open with no FileShare.Delete so File.Delete should fail, but allow reads so upload can succeed.
				lockStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

				// Act
				var result = device.CopyFile(FileCopyMode.Upload, sourcePath, targetDevicePath, skipExisting: false, isMove: true);

				// Assert
				Assert.True(mock.FileExists(targetDevicePath));
				Assert.True(File.Exists(sourcePath));
				Assert.False(result.SourceDeleted);
			}
			finally
			{
				lockStream?.Dispose();
				try
				{
					if (File.Exists(sourcePath))
					{
						File.Delete(sourcePath);
					}
				}
				catch
				{
					// ignore cleanup failures
				}
			}
		}

		private sealed class TempDirectory : IDisposable
		{
			public string Path { get; }

			public TempDirectory()
			{
				Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CopyFileEdgeCaseTests_" + Guid.NewGuid().ToString("N"));
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
		/// Minimal IMediaDevice implementation to pin current move semantics when local source doesn't exist.
		/// UploadFile does not read the local path, so CopyFile can reach its move/delete block.
		/// </summary>
		private sealed class UploadIgnoresMissingSourceMock : IMediaDevice
		{
			private readonly Dictionary<string, byte[]> _files = new();

			public bool IsConnected { get; private set; }
			public string FriendlyName => "UploadIgnoresMissingSourceMock";

			public void Connect() => IsConnected = true;
			public void Dispose() { }

			public bool FileExists(string path) => _files.ContainsKey(path);
			public bool DirectoryExists(string folder) => true;
			public void CreateDirectory(string folder) { }
			public string[] GetDirectories(string folder) => Array.Empty<string>();
			public string[] GetFiles(string folder) => Array.Empty<string>();

			public MediaFileInfo GetFileInfo(string path) => throw new NotImplementedException();

			public void DownloadFile(string sourceFilePath, string targetFilePath)
			{
				File.WriteAllBytes(targetFilePath, _files[sourceFilePath]);
			}

			public void UploadFile(string sourceFilePath, string targetFilePath)
			{
				// Intentionally ignore source file existence.
				_files[targetFilePath] = Array.Empty<byte>();
			}

			public void DeleteFile(string path)
			{
				_files.Remove(path);
			}

			public FileComparisonInfo InternalGetComparisonInfo(string path)
			{
				_files.TryGetValue(path, out var bytes);
				return new FileComparisonInfo
				{
					Length = (ulong)(bytes?.Length ?? 0),
					ModifiedDate = DateTime.Now
				};
			}
		}
	}
}
