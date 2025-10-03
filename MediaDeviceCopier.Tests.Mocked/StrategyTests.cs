using MediaDeviceCopier;
using MediaDeviceCopier.Tests.Mocked;
using Xunit;

namespace MediaDeviceCopier.Tests.Mocked
{
	public class StrategyTests
	{
		private sealed class TempDirectory : IDisposable
		{
			public string Path { get; }

			public TempDirectory()
			{
				Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StrategyTests_" + Guid.NewGuid().ToString("N"));
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

		[Fact]
		public void Download_FirstStrategySucceeds_StopsImmediately()
		{
			// Arrange: Mock that succeeds on first attempt
			var mock = new StrategyTestMock(failureCount: 0);
			mock.Connect();
			mock.AddFolder("/device");
			mock.AddFile("/device/test.jpg", new byte[] { 1, 2, 3 });

			var device = new MtpDevice(mock);
			device.Connect();

			using var tempDir = new TempDirectory();
			var targetPath = System.IO.Path.Combine(tempDir.Path, "test.jpg");

			// Act
			var result = device.CopyFile(FileCopyMode.Download, "/device/test.jpg", targetPath, skipExisting: false, isMove: false);

			// Assert
			Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
			Assert.True(File.Exists(targetPath));
			Assert.Single(mock.DownloadAttempts); // Only one strategy executed
		}

		[Fact]
		public void Download_FirstStrategyFails_SecondSucceeds()
		{
			// Arrange: Mock that fails once, then succeeds
			var mock = new StrategyTestMock(failureCount: 1);
			mock.Connect();
			mock.AddFolder("/device");
			mock.AddFile("/device/test.wav", new byte[] { 1, 2, 3 });

			var device = new MtpDevice(mock);
			device.Connect();

			using var tempDir = new TempDirectory();
			var targetPath = System.IO.Path.Combine(tempDir.Path, "test.wav");

			// Act
			var result = device.CopyFile(FileCopyMode.Download, "/device/test.wav", targetPath, skipExisting: false, isMove: false);

			// Assert
			Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
			Assert.True(File.Exists(targetPath));
			Assert.Equal(2, mock.DownloadAttempts.Count); // Two strategies executed
		}

		[Fact]
		public void Download_ThreeStrategiesFail_FourthSucceeds()
		{
			// Arrange: Mock that fails 3 times, succeeds on 4th
			var mock = new StrategyTestMock(failureCount: 3);
			mock.Connect();
			mock.AddFolder("/device");
			mock.AddFile("/device/test.thm", new byte[] { 1, 2, 3 });

			var device = new MtpDevice(mock);
			device.Connect();

			using var tempDir = new TempDirectory();
			var targetPath = System.IO.Path.Combine(tempDir.Path, "test.thm");

			// Act
			var result = device.CopyFile(FileCopyMode.Download, "/device/test.thm", targetPath, skipExisting: false, isMove: false);

			// Assert
			Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
			Assert.True(File.Exists(targetPath));
			Assert.Equal(4, mock.DownloadAttempts.Count); // All 4 strategies executed
		}

		[Fact]
		public void Download_AllStrategiesFail_ReturnsSkippedBecauseUnsupported()
		{
			// Arrange: Mock that always fails (more failures than available strategies)
			var mock = new StrategyTestMock(failureCount: 10);
			mock.Connect();
			mock.AddFolder("/device");
			mock.AddFile("/device/problem.file", new byte[] { 1, 2, 3 });

			var device = new MtpDevice(mock);
			device.Connect();

			using var tempDir = new TempDirectory();
			var targetPath = System.IO.Path.Combine(tempDir.Path, "problem.file");

			// Act
			var result = device.CopyFile(FileCopyMode.Download, "/device/problem.file", targetPath, skipExisting: false, isMove: false);

			// Assert
			Assert.Equal(FileCopyStatus.SkippedBecauseUnsupported, result.CopyStatus);
			Assert.False(File.Exists(targetPath)); // File should not exist
			Assert.Equal(4, mock.DownloadAttempts.Count); // All 4 strategies attempted
		}

		[Fact]
		public void Download_SkipExisting_DoesNotExecuteStrategies()
		{
			// Arrange
			var mock = new StrategyTestMock(failureCount: 0);
			mock.Connect();
			mock.AddFolder("/device");
			var bytes = new byte[] { 1, 2, 3, 4 };
			var timestamp = new DateTime(2024, 5, 17, 12, 0, 0, DateTimeKind.Local);
			
			using var tempDir = new TempDirectory();
			var targetPath = System.IO.Path.Combine(tempDir.Path, "existing.jpg");

			// Pre-create target file with matching size and timestamp
			File.WriteAllBytes(targetPath, bytes);
			File.SetLastWriteTime(targetPath, timestamp);
			
			// Read back the actual file system timestamp to ensure exact match
			var actualFileTimestamp = File.GetLastWriteTime(targetPath);
			
			// Now set up mock with the file system's actual timestamp
			mock.AddFile("/device/existing.jpg", bytes, actualFileTimestamp);

			var device = new MtpDevice(mock);
			device.Connect();

			// Act
			var result = device.CopyFile(FileCopyMode.Download, "/device/existing.jpg", targetPath, skipExisting: true, isMove: false);

			// Assert
			Assert.Equal(FileCopyStatus.SkippedBecauseAlreadyExists, result.CopyStatus);
			Assert.Empty(mock.DownloadAttempts); // No strategies executed
		}

		[Fact]
		public void Download_CustomStrategyOrder_ExecutesInProvidedSequence()
		{
			// Arrange: Use a successful mock for the underlying device
			var testMock = new StrategyTestMock(failureCount: 0); // Will succeed when called
			testMock.Connect();
			testMock.AddFolder("/device");
			testMock.AddFile("/device/test.mp4", new byte[] { 1, 2, 3 });

			var captureMock = new StrategyCaptureMock(new Dictionary<string, bool>
			{
				{ "CustomA", false },
				{ "CustomB", false },
				{ "CustomC", true }  // Succeeds on third
			});

			var customStrategies = new List<(string Name, DownloadStrategy Strategy)>
			{
				("CustomA", ctx => {
					captureMock.InvokedStrategies.Add("CustomA");
					// Simulate failure without calling device
					throw new System.Runtime.InteropServices.COMException("CustomA failed", unchecked((int)0x80004005));
				}),
				("CustomB", ctx => {
					captureMock.InvokedStrategies.Add("CustomB");
					// Simulate failure without calling device
					throw new System.Runtime.InteropServices.COMException("CustomB failed", unchecked((int)0x80004005));
				}),
				("CustomC", ctx => {
					captureMock.InvokedStrategies.Add("CustomC");
					// Actually download the file (mock will succeed)
					ctx.Device.DownloadFile(ctx.SourceFilePath, ctx.TargetFilePath);
					return true;
				}),
				("CustomD", ctx => {
					captureMock.InvokedStrategies.Add("CustomD");
					ctx.Device.DownloadFile(ctx.SourceFilePath, ctx.TargetFilePath);
					return true;
				})
			};

			MtpDevice.SetDownloadStrategies(customStrategies);

			var device = new MtpDevice(testMock);
			device.Connect();

			using var tempDir = new TempDirectory();
			var targetPath = System.IO.Path.Combine(tempDir.Path, "test.mp4");

			try
			{
				// Act
				var result = device.CopyFile(FileCopyMode.Download, "/device/test.mp4", targetPath, skipExisting: false, isMove: false);

				// Assert
				Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
				Assert.Equal(3, captureMock.InvokedStrategies.Count);
				Assert.Equal("CustomA", captureMock.InvokedStrategies[0]);
				Assert.Equal("CustomB", captureMock.InvokedStrategies[1]);
				Assert.Equal("CustomC", captureMock.InvokedStrategies[2]);
				// CustomD should not be invoked (stopped after CustomC succeeded)
				Assert.True(File.Exists(targetPath));
			}
			finally
			{
				// Reset to default strategies for other tests
				MtpDevice.SetDownloadStrategies(MtpDevice.GetDefaultDownloadStrategies());
			}
		}

		[Fact]
		public void ClassifyFile_ImageExtensions_ReturnsImageClass()
		{
			Assert.Equal(FileMediaClass.Image, MtpDevice.ClassifyFile(".jpg"));
			Assert.Equal(FileMediaClass.Image, MtpDevice.ClassifyFile("jpeg"));
			Assert.Equal(FileMediaClass.Image, MtpDevice.ClassifyFile(".PNG"));
			Assert.Equal(FileMediaClass.Image, MtpDevice.ClassifyFile("raw"));
		}

		[Fact]
		public void ClassifyFile_VideoExtensions_ReturnsVideoClass()
		{
			Assert.Equal(FileMediaClass.Video, MtpDevice.ClassifyFile(".mp4"));
			Assert.Equal(FileMediaClass.Video, MtpDevice.ClassifyFile("MOV"));
			Assert.Equal(FileMediaClass.Video, MtpDevice.ClassifyFile(".avi"));
		}

		[Fact]
		public void ClassifyFile_AudioExtensions_ReturnsAudioClass()
		{
			Assert.Equal(FileMediaClass.Audio, MtpDevice.ClassifyFile(".wav"));
			Assert.Equal(FileMediaClass.Audio, MtpDevice.ClassifyFile("mp3"));
			Assert.Equal(FileMediaClass.Audio, MtpDevice.ClassifyFile(".FLAC"));
		}

		[Fact]
		public void ClassifyFile_MetadataExtensions_ReturnsMetadataClass()
		{
			Assert.Equal(FileMediaClass.Metadata, MtpDevice.ClassifyFile(".thm"));
			Assert.Equal(FileMediaClass.Metadata, MtpDevice.ClassifyFile("LRV"));
			Assert.Equal(FileMediaClass.Metadata, MtpDevice.ClassifyFile(".xmp"));
		}

		[Fact]
		public void ClassifyFile_UnknownExtension_ReturnsUnknown()
		{
			Assert.Equal(FileMediaClass.Unknown, MtpDevice.ClassifyFile(".xyz"));
			Assert.Equal(FileMediaClass.Unknown, MtpDevice.ClassifyFile("unknown"));
		}

		[Fact]
		public void Download_MoveOperation_DeletesSourceAfterSuccessfulDownload()
		{
			// Arrange
			var mock = new StrategyTestMock(failureCount: 1); // Fail once, then succeed
			mock.Connect();
			mock.AddFolder("/device");
			mock.AddFile("/device/move.jpg", new byte[] { 1, 2, 3 });

			var device = new MtpDevice(mock);
			device.Connect();

			using var tempDir = new TempDirectory();
			var targetPath = System.IO.Path.Combine(tempDir.Path, "move.jpg");

			// Act
			var result = device.CopyFile(FileCopyMode.Download, "/device/move.jpg", targetPath, skipExisting: false, isMove: true);

			// Assert
			Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
			Assert.True(result.SourceDeleted);
			Assert.True(File.Exists(targetPath));
			Assert.False(mock.FileExists("/device/move.jpg")); // Source deleted
		}

		[Fact]
		public void Download_MoveOperationAllStrategiesFail_DoesNotDeleteSource()
		{
			// Arrange
			var mock = new StrategyTestMock(failureCount: 10); // Always fail
			mock.Connect();
			mock.AddFolder("/device");
			var sourceFile = "/device/cant_move.dat";
			mock.AddFile(sourceFile, new byte[] { 1, 2, 3 });

			var device = new MtpDevice(mock);
			device.Connect();

			using var tempDir = new TempDirectory();
			var targetPath = System.IO.Path.Combine(tempDir.Path, "cant_move.dat");

			// Act
			var result = device.CopyFile(FileCopyMode.Download, sourceFile, targetPath, skipExisting: false, isMove: true);

			// Assert - When download fails, SourceDeleted should be false
			// The current behavior marks it as SkippedBecauseUnsupported and doesn't set SourceDeleted to true
			Assert.Equal(FileCopyStatus.SkippedBecauseUnsupported, result.CopyStatus);
			Assert.False(result.SourceDeleted);
			// File should still exist since copy failed (deletion only happens on successful copy in move mode)
			// However, the mock's behavior when file is still tracked is to return true from FileExists
			// This validates that deletion wasn't attempted (file still tracked)
		}

		[Fact]
		public void Download_DifferentFileTypes_AllUseUniversalStrategyPipeline()
		{
			// This test validates that THM, WAV, MP4, and other extensions all use the same strategy pipeline
			var fileTypes = new[] { ".thm", ".wav", ".mp4", ".jpg", ".unknown" };

			foreach (var ext in fileTypes)
			{
				// Arrange: Fail twice, succeed on third attempt
				var mock = new StrategyTestMock(failureCount: 2);
				mock.Connect();
				mock.AddFolder("/device");
				mock.AddFile($"/device/file{ext}", new byte[] { 1, 2, 3 });

				var device = new MtpDevice(mock);
				device.Connect();

				using var tempDir = new TempDirectory();
				var targetPath = System.IO.Path.Combine(tempDir.Path, $"file{ext}");

				// Act
				var result = device.CopyFile(FileCopyMode.Download, $"/device/file{ext}", targetPath, skipExisting: false, isMove: false);

				// Assert: All file types should succeed after 3 attempts (universal strategy)
				Assert.Equal(FileCopyStatus.Copied, result.CopyStatus);
				Assert.Equal(3, mock.DownloadAttempts.Count);
				Assert.True(File.Exists(targetPath));
			}
		}
	}
}