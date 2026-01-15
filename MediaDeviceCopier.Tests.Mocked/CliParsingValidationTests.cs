using MediaDeviceCopier;
using System.Text;
using Xunit;

namespace MediaDeviceCopier.Tests.Mocked;

[Collection(nameof(MtpDeviceStaticStateCollection))]
public sealed class CliParsingValidationTests
{
	private sealed class ConsoleCapture : IDisposable
	{
		private readonly TextWriter _originalOut;
		private readonly TextWriter _originalError;
		private readonly StringWriter _out;
		private readonly StringWriter _error;

		public ConsoleCapture()
		{
			_originalOut = Console.Out;
			_originalError = Console.Error;
			_out = new StringWriter(new StringBuilder());
			_error = new StringWriter(new StringBuilder());

			Console.SetOut(_out);
			Console.SetError(_error);
		}

		public string StdOut => _out.ToString();
		public string StdErr => _error.ToString();

		public void Dispose()
		{
			Console.SetOut(_originalOut);
			Console.SetError(_originalError);
			_out.Dispose();
			_error.Dispose();
		}
	}

	private sealed class TempDirectory : IDisposable
	{
		public string Path { get; }

		public TempDirectory()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CliParsingValidationTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path);
		}

		public void Dispose()
		{
			try
			{
				if (Directory.Exists(Path))
					Directory.Delete(Path, recursive: true);
			}
			catch
			{
				// ignore cleanup failures
			}
		}
	}

	private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeProgramAsync(
		string[] args,
		Func<IEnumerable<IMediaDevice>> deviceFactory)
	{
		var originalFactory = MtpDevice.DeviceFactory;
		try
		{
			MtpDevice.DeviceFactory = deviceFactory;

			using var capture = new ConsoleCapture();
			var exitCode = await Program.Main(args);
			var stdout = capture.StdOut;
			var stderr = capture.StdErr;
			return (exitCode, stdout, stderr);
		}
		finally
		{
			// Reset static state for other tests (also clears cached device list)
			MtpDevice.DeviceFactory = originalFactory;
		}
	}

	[Fact]
	public async Task ListFiles_InvalidFileRegex_IsRejectedByValidator()
	{
		var (exitCode, stdout, stderr) = await InvokeProgramAsync(
			new[] { "list-files", "-n", "MockDevice", "-s", "/DCIM", "-f", "[" },
			deviceFactory: () => throw new InvalidOperationException("DeviceFactory should not be called for parse/validation failures."));

		Assert.NotEqual(0, exitCode);
		Assert.Contains("Invalid file regex pattern", stdout + stderr);
	}

	[Fact]
	public async Task UploadFiles_InvalidSubfolderRegex_IsRejectedByValidator()
	{
		using var temp = new TempDirectory();

		var (exitCode, stdout, stderr) = await InvokeProgramAsync(
			new[] { "upload-files", "-n", "MockDevice", "-s", temp.Path, "-t", temp.Path, "-sf", "[" },
			deviceFactory: () => throw new InvalidOperationException("DeviceFactory should not be called for parse/validation failures."));

		Assert.NotEqual(0, exitCode);
		Assert.Contains("Invalid subfolder regex pattern", stdout + stderr);
	}

	[Fact]
	public async Task ListFiles_MissingRequiredOptions_ReturnsNonZero_AndMentionsRequiredOptions()
	{
		var (exitCode, stdout, stderr) = await InvokeProgramAsync(
			new[] { "list-files" },
			deviceFactory: () => throw new InvalidOperationException("DeviceFactory should not be called for parse/validation failures."));

		Assert.NotEqual(0, exitCode);

		var combined = (stdout + stderr).ToLowerInvariant();
		Assert.Contains("--device-name", combined);
		Assert.Contains("--source-folder", combined);
	}

	[Fact]
	public async Task DownloadFiles_SkipExistingOptionOmitted_DefaultsToTrue_AndSkipsExistingFile()
	{
		// Arrange: device has a file; local target already exists and matches length+timestamp.
		var device = new MockMediaDevice { FriendlyName = "MockDevice" };
		device.AddFolder("/device");
		var sourceFilePath = "/device/file.bin";

		using var temp = new TempDirectory();
		var localTargetPath = System.IO.Path.Combine(temp.Path, "file.bin");
		var content = new byte[] { 1, 2, 3, 4 };

		File.WriteAllBytes(localTargetPath, content);
		File.SetLastWriteTime(localTargetPath, new DateTime(2025, 01, 02, 03, 04, 05));
		var localTimestamp = File.GetLastWriteTime(localTargetPath);

		// Ensure MTP comparison info matches the local file.
		device.AddFile(sourceFilePath, content, timestamp: localTimestamp);

		// Act: omit --skip-existing (so skipExisting == null in parsing)
		var (exitCode, stdout, stderr) = await InvokeProgramAsync(
			new[] { "download-files", "-n", "MockDevice", "-s", "/device", "-t", temp.Path },
			deviceFactory: () => new[] { (IMediaDevice)device });

		// Assert
		Assert.Equal(0, exitCode);
		Assert.Empty(stderr);
		Assert.Contains("skipped (already exists)", stdout);
	}
}
