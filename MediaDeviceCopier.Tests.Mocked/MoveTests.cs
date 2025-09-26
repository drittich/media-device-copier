using MediaDeviceCopier;
using MediaDeviceCopier.Tests.Mocked;
using Xunit;

namespace MediaDeviceCopier.Tests.Mocked
{
	public class MoveTests
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
		public void MoveDownload_DeletesSourceOnDevice()
		{
			// Arrange
			var (device, mock) = CreateConnectedMock();
			var sourcePath = "/device/file1.bin";
			var content = new byte[] { 1, 2, 3, 4 };
			mock.AddFolder("/device");
			mock.AddFile(sourcePath, content);
			using var tempDir = new TempDirectory();
			var targetPath = Path.Combine(tempDir.Path, "file1.bin");

			// Act
			var result = device.CopyFile(FileCopyMode.Download, sourcePath, targetPath, skipExisting: false, isMove: true);

			// Assert
			Assert.True(File.Exists(targetPath));
			Assert.False(mock.FileExists(sourcePath));
			Assert.True(result.SourceDeleted);
			Assert.Equal((ulong)content.Length, result.Length);
		}

		[Fact]
		public void MoveUpload_DeletesLocalSourceFile()
		{
			// Arrange
			var (device, mock) = CreateConnectedMock();
			using var tempDir = new TempDirectory();
			var sourceFile = Path.Combine(tempDir.Path, "upload.bin");
			File.WriteAllBytes(sourceFile, new byte[] { 10, 20, 30 });
			var targetDevicePath = "/up/upload.bin";

			// Act
			var result = device.CopyFile(FileCopyMode.Upload, sourceFile, targetDevicePath, skipExisting: false, isMove: true);

			// Assert
			Assert.False(File.Exists(sourceFile));
			Assert.True(mock.FileExists(targetDevicePath));
			Assert.True(result.SourceDeleted);
			Assert.Equal<ulong>(3, result.Length);
		}

		[Fact]
		public void MoveDownload_DeleteFailure_SourceDeletedFalse()
		{
			// Arrange
			var (device, mock) = CreateConnectedMock();
			var sourcePath = "/device/file2.bin";
			mock.AddFolder("/device");
			mock.AddFile(sourcePath, new byte[] { 5, 6, 7 });
			mock.ThrowOnDelete = true;
			using var tempDir = new TempDirectory();
			var targetPath = Path.Combine(tempDir.Path, "file2.bin");

			// Act
			var result = device.CopyFile(FileCopyMode.Download, sourcePath, targetPath, skipExisting: false, isMove: true);

			// Assert
			Assert.True(File.Exists(targetPath));
			// Source still exists because delete failed
			Assert.True(mock.FileExists(sourcePath));
			Assert.False(result.SourceDeleted);
		}

		[Fact]
		public void MoveDownload_SkipExisting_DoesNotDeleteSource()
		{
			// Arrange
			var (device, mock) = CreateConnectedMock();
			mock.AddFolder("/device");
			var sourcePath = "/device/file_skip.bin";
			var bytes = new byte[] { 9, 9, 9, 9 };
			var timestamp = new DateTime(2024, 5, 17, 12, 0, 0, DateTimeKind.Local);
			mock.AddFile(sourcePath, bytes, timestamp);

			using var tempDir = new TempDirectory();
			var targetPath = Path.Combine(tempDir.Path, "file_skip.bin");
			File.WriteAllBytes(targetPath, bytes);
			// Align last write time so comparison (length+date) succeeds
			File.SetLastWriteTime(targetPath, timestamp);

			// Act
			var result = device.CopyFile(FileCopyMode.Download, sourcePath, targetPath, skipExisting: true, isMove: true);

			// Assert
			Assert.True(File.Exists(targetPath));
			Assert.True(mock.FileExists(sourcePath)); // not deleted because skipped
			Assert.False(result.SourceDeleted);
			Assert.Equal(FileCopyStatus.SkippedBecauseAlreadyExists, result.CopyStatus);
		}

		private sealed class TempDirectory : IDisposable
		{
			public string Path { get; }
			public TempDirectory()
			{
				Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MoveTests_" + Guid.NewGuid().ToString("N"));
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
	}
}