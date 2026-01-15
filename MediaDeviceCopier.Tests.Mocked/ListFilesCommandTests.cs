using MediaDeviceCopier;
using MediaDevices;
using System.Text;
using Xunit;

namespace MediaDeviceCopier.Tests.Mocked;

[CollectionDefinition(nameof(MtpDeviceStaticStateCollection), DisableParallelization = true)]
public sealed class MtpDeviceStaticStateCollection : ICollectionFixture<MtpDeviceStaticStateFixture>
{
}

/// <summary>
/// Ensures tests that mutate MtpDevice static state (DeviceFactory cache) and Console output do not
/// run in parallel with each other.
/// </summary>
public sealed class MtpDeviceStaticStateFixture
{
}

[Collection(nameof(MtpDeviceStaticStateCollection))]
public sealed class ListFilesCommandTests
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

		public string[] GetStdOutLines()
		{
			return StdOut
				.Replace("\r\n", "\n")
				.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		}

		public void Dispose()
		{
			Console.SetOut(_originalOut);
			Console.SetError(_originalError);
			_out.Dispose();
			_error.Dispose();
		}
	}

	private static async Task<int> InvokeProgramWithDeviceAsync(MockMediaDevice device, string[] args)
	{
		var originalFactory = MtpDevice.DeviceFactory;
		try
		{
			MtpDevice.DeviceFactory = () => new[] { (IMediaDevice)device };
			return await Program.Main(args);
		}
		finally
		{
			// Reset static state (clears cached device list as a side-effect)
			MtpDevice.DeviceFactory = originalFactory;
		}
	}

	[Fact]
	public async Task ListFiles_PrintsSortedFileNames_OnePerLine()
	{
		var device = new MockMediaDevice { FriendlyName = "MockDevice" };
		device.AddFolder("/DCIM");
		device.AddFile("/DCIM/B.jpg", new byte[] { 1 });
		device.AddFile("/DCIM/a.PNG", new byte[] { 2 });
		device.AddFile("/DCIM/c.txt", new byte[] { 3 });

		using var capture = new ConsoleCapture();
		var exitCode = await InvokeProgramWithDeviceAsync(device, new[]
		{
			"list-files",
			"-n", "MockDevice",
			"-s", "/DCIM",
		});

		Assert.Equal(0, exitCode);
		Assert.Empty(capture.StdErr);
		Assert.Equal(new[] { "a.PNG", "B.jpg", "c.txt" }, capture.GetStdOutLines());
	}

	[Fact]
	public async Task ListFiles_FilterFilesRegex_FiltersByFilenameNotFullPath()
	{
		var device = new MockMediaDevice { FriendlyName = "MockDevice" };
		device.AddFolder("/DCIM");
		device.AddFile("/DCIM/image.jpg", new byte[] { 1 });
		device.AddFile("/DCIM/graphic.png", new byte[] { 2 });
		device.AddFile("/DCIM/document.txt", new byte[] { 3 });

		// Path contains ".jpg" in a folder name; should NOT match because filtering is on file name only.
		device.AddFile("/DCIM/folder.jpg/not-match.txt", new byte[] { 4 });

		using var capture = new ConsoleCapture();
		var exitCode = await InvokeProgramWithDeviceAsync(device, new[]
		{
			"list-files",
			"-n", "MockDevice",
			"-s", "/DCIM",
			"-f", "\\.(jpg|png)$",
		});

		Assert.Equal(0, exitCode);
		Assert.Empty(capture.StdErr);
		Assert.Equal(new[] { "graphic.png", "image.jpg" }, capture.GetStdOutLines());
	}

	[Fact]
	public async Task ListFiles_FullPath_PrintsRawPathsReturnedByDevice()
	{
		var device = new MockMediaDevice { FriendlyName = "MockDevice" };
		device.AddFolder("/DCIM");
		device.AddFile("/DCIM/b/File2.txt", new byte[] { 1 });
		device.AddFile("/DCIM/A/file1.txt", new byte[] { 2 });
		device.AddFile("/DCIM/c/file3.txt", new byte[] { 3 });

		using var capture = new ConsoleCapture();
		var exitCode = await InvokeProgramWithDeviceAsync(device, new[]
		{
			"list-files",
			"-n", "MockDevice",
			"-s", "/DCIM",
			"--full-path",
		});

		Assert.Equal(0, exitCode);
		Assert.Empty(capture.StdErr);
		Assert.Equal(
			new[] { "/DCIM/A/file1.txt", "/DCIM/b/File2.txt", "/DCIM/c/file3.txt" },
			capture.GetStdOutLines());
	}
}
