using MediaDevices;
using System.Runtime.InteropServices;
using System.Text;

namespace MediaDeviceCopier.Tests.Mocked;

/// <summary>
/// In-memory device with a folder tree whose folder enumeration can be made to fail.
/// Paths use '/' separators, e.g. "/device/A/photo.jpg". A path ending in '/' is an entry with an empty name.
/// </summary>
internal sealed class FakeTreeDevice : IMediaDevice
{
	private readonly HashSet<string> _folders = new();
	private readonly Dictionary<string, byte[]> _files = new();
	private readonly Dictionary<string, (Exception Exception, int Times)> _failures = new();
	private readonly Dictionary<string, int> _calls = new();

	public bool IsConnected { get; private set; }
	public string FriendlyName { get; init; } = "MockDevice";
	public List<string> DeletedFiles { get; } = new();

	public void Connect() => IsConnected = true;

	public void AddFolder(string folder) => _folders.Add(folder);
	public void AddFile(string path, byte[] content) => _files[path] = content;

	public void FailGetFiles(string folder, Exception exception, int times = int.MaxValue) => _failures[$"GetFiles:{folder}"] = (exception, times);
	public void FailGetDirectories(string folder, Exception exception, int times = int.MaxValue) => _failures[$"GetDirectories:{folder}"] = (exception, times);
	public int GetFilesCalls(string folder) => _calls.GetValueOrDefault($"GetFiles:{folder}");
	public int GetDirectoriesCalls(string folder) => _calls.GetValueOrDefault($"GetDirectories:{folder}");

	private void ThrowIfFailing(string key)
	{
		_calls[key] = _calls.GetValueOrDefault(key) + 1;
		if (_failures.TryGetValue(key, out var failure) && failure.Times > 0)
		{
			_failures[key] = (failure.Exception, failure.Times == int.MaxValue ? int.MaxValue : failure.Times - 1);
			throw failure.Exception;
		}
	}

	private static string Parent(string path) => path.Substring(0, path.LastIndexOf('/'));

	public bool DirectoryExists(string folder) => _folders.Contains(folder);

	public string[] GetDirectories(string folder)
	{
		ThrowIfFailing($"GetDirectories:{folder}");
		return _folders.Where(f => Parent(f) == folder).ToArray();
	}

	public string[] GetFiles(string folder)
	{
		ThrowIfFailing($"GetFiles:{folder}");
		return _files.Keys.Where(f => Parent(f) == folder).ToArray();
	}

	public bool FileExists(string path) => _files.ContainsKey(path);
	public void DownloadFile(string sourceFilePath, string targetFilePath) => File.WriteAllBytes(targetFilePath, _files[sourceFilePath]);

	public void DeleteFile(string path)
	{
		DeletedFiles.Add(path);
		_files.Remove(path);
	}

	// Picked up by MtpDevice via reflection so MediaFileInfo (which cannot be constructed here) is not needed
	public FileComparisonInfo InternalGetComparisonInfo(string path) => new()
	{
		Length = (ulong)_files[path].Length,
		ModifiedDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local)
	};

	public MediaFileInfo GetFileInfo(string path) => throw new NotImplementedException();
	public void UploadFile(string sourceFilePath, string targetFilePath) => throw new NotImplementedException();
	public void CreateDirectory(string folder) => throw new NotImplementedException();
	public void Dispose() { }
}

[Collection(nameof(MtpDeviceStaticStateCollection))]
public sealed class EnumerationAndFailureHandlingTests : IDisposable
{
	private static readonly int ElementNotFound = unchecked((int)0x80070490);
	private static readonly int InvalidData = unchecked((int)0x8007000D);
	private static readonly int GenericFailure = unchecked((int)0x80004005);

	private readonly int _originalMaxAttempts = MtpDevice.EnumerationMaxAttempts;
	private readonly TimeSpan _originalRetryDelay = MtpDevice.EnumerationRetryDelay;

	public EnumerationAndFailureHandlingTests()
	{
		// Keep retry tests fast
		MtpDevice.EnumerationRetryDelay = TimeSpan.Zero;
	}

	public void Dispose()
	{
		MtpDevice.EnumerationMaxAttempts = _originalMaxAttempts;
		MtpDevice.EnumerationRetryDelay = _originalRetryDelay;
	}

	private sealed class TempDirectory : IDisposable
	{
		public string Path { get; }

		public TempDirectory()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EnumerationTests_" + Guid.NewGuid().ToString("N"));
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

	private static async Task<(int ExitCode, string Output)> RunProgramAsync(FakeTreeDevice device, params string[] args)
	{
		var originalFactory = MtpDevice.DeviceFactory;
		var originalOut = Console.Out;
		var output = new StringWriter(new StringBuilder());
		try
		{
			MtpDevice.DeviceFactory = () => new[] { (IMediaDevice)device };
			Console.SetOut(output);
			var exitCode = await Program.Main(args);
			return (exitCode, output.ToString());
		}
		finally
		{
			Console.SetOut(originalOut);
			// Reset static state (clears cached device list as a side-effect)
			MtpDevice.DeviceFactory = originalFactory;
		}
	}

	private static COMException Com(int hresult, string message = "device error") => new(message, hresult);

	private static MtpDevice ConnectedMtpDevice(FakeTreeDevice fake)
	{
		fake.Connect();
		return new MtpDevice(fake);
	}

	#region Empty folders (0x80070490)

	[Fact]
	public void GetFiles_ElementNotFound_ReturnsEmpty()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.FailGetFiles("/device", Com(ElementNotFound));

		var files = ConnectedMtpDevice(fake).GetFiles("/device");

		Assert.Empty(files);
	}

	[Fact]
	public void GetDirectories_ElementNotFound_ReturnsEmpty()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.FailGetDirectories("/device", Com(ElementNotFound));

		var directories = ConnectedMtpDevice(fake).GetDirectories("/device");

		Assert.Empty(directories);
	}

	[Fact]
	public async Task ListFiles_EmptyFolder_ExitsZeroAndPrintsNothing()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/DCIM");
		fake.FailGetFiles("/DCIM", Com(ElementNotFound));

		var (exitCode, output) = await RunProgramAsync(fake, "list-files", "-n", "MockDevice", "-s", "/DCIM");

		Assert.Equal(0, exitCode);
		Assert.Equal(string.Empty, output.Trim());
	}

	[Fact]
	public async Task DownloadRecursive_EmptyFolder_DoesNotAbortRun()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.AddFolder("/device/A");
		fake.AddFolder("/device/B");
		fake.AddFile("/device/B/b.jpg", new byte[] { 1, 2, 3 });
		fake.FailGetFiles("/device/A", Com(ElementNotFound));
		fake.FailGetDirectories("/device/A", Com(ElementNotFound));

		using var target = new TempDirectory();
		var (exitCode, _) = await RunProgramAsync(fake, "download-files", "-n", "MockDevice", "-s", "/device", "-t", target.Path, "-r");

		Assert.Equal(0, exitCode);
		Assert.True(File.Exists(Path.Combine(target.Path, "B", "b.jpg")));
	}

	#endregion

	#region Transient enumeration errors (0x8007000D)

	[Fact]
	public void GetDirectories_TransientError_IsRetriedThenSucceeds()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.AddFolder("/device/A");
		fake.FailGetDirectories("/device", Com(InvalidData), times: 2);

		var directories = ConnectedMtpDevice(fake).GetDirectories("/device");

		Assert.Equal(new[] { "/device/A" }, directories);
		Assert.Equal(3, fake.GetDirectoriesCalls("/device"));
	}

	[Fact]
	public void GetFiles_PersistentTransientError_ThrowsAfterMaxAttempts()
	{
		MtpDevice.EnumerationMaxAttempts = 3;
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.FailGetFiles("/device", Com(InvalidData));

		var ex = Assert.Throws<COMException>(() => ConnectedMtpDevice(fake).GetFiles("/device"));

		Assert.Equal(InvalidData, ex.HResult);
		Assert.Equal(3, fake.GetFilesCalls("/device"));
	}

	[Fact]
	public void GetFiles_NonTransientComError_IsNotRetried()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.FailGetFiles("/device", Com(GenericFailure));

		var ex = Assert.Throws<COMException>(() => ConnectedMtpDevice(fake).GetFiles("/device"));

		Assert.Equal(GenericFailure, ex.HResult);
		Assert.Equal(1, fake.GetFilesCalls("/device"));
	}

	[Fact]
	public async Task DownloadRecursive_TransientFolderError_RecoversAndExitsZero()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.AddFolder("/device/A");
		fake.AddFile("/device/A/a.jpg", new byte[] { 1 });
		fake.FailGetDirectories("/device/A", Com(InvalidData), times: 2);

		using var target = new TempDirectory();
		var (exitCode, _) = await RunProgramAsync(fake, "download-files", "-n", "MockDevice", "-s", "/device", "-t", target.Path, "-r");

		Assert.Equal(0, exitCode);
		Assert.True(File.Exists(Path.Combine(target.Path, "A", "a.jpg")));
	}

	[Fact]
	public async Task DownloadRecursive_FolderFailsPersistently_ContinuesAndReportsFailure()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.AddFolder("/device/A");
		fake.AddFolder("/device/B");
		fake.AddFolder("/device/C");
		fake.AddFile("/device/A/a.jpg", new byte[] { 1 });
		fake.AddFile("/device/B/b.jpg", new byte[] { 2 });
		fake.AddFile("/device/C/c.jpg", new byte[] { 3 });
		fake.FailGetDirectories("/device/B", Com(InvalidData));

		using var target = new TempDirectory();
		var (exitCode, output) = await RunProgramAsync(fake, "download-files", "-n", "MockDevice", "-s", "/device", "-t", target.Path, "-r");

		Assert.Equal(1, exitCode);
		Assert.True(File.Exists(Path.Combine(target.Path, "A", "a.jpg")));
		Assert.False(File.Exists(Path.Combine(target.Path, "B", "b.jpg")));
		Assert.True(File.Exists(Path.Combine(target.Path, "C", "c.jpg")));
		Assert.Contains("Continuing with the next folder", output);
		Assert.Contains("Completed with errors: 0 file(s) and 1 folder(s)", output);
		Assert.Contains("/device/B", output);
	}

	[Fact]
	public async Task DownloadRecursive_RootFolderFailsPersistently_ReportsFailureAndExitsNonZero()
	{
		// A persistent device error on the root folder itself must still produce a summary and exit 1,
		// not escape as an unhandled exception.
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.FailGetDirectories("/device", Com(InvalidData));

		using var target = new TempDirectory();
		var (exitCode, output) = await RunProgramAsync(fake, "download-files", "-n", "MockDevice", "-s", "/device", "-t", target.Path, "-r");

		Assert.Equal(1, exitCode);
		Assert.Contains("could not process folder /device", output);
		Assert.Contains("Completed with errors: 0 file(s) and 1 folder(s)", output);
		Assert.Contains("/device", output);
	}

	[Fact]
	public async Task DownloadNonRecursive_RootFolderFileEnumerationFailsPersistently_ReportsFailureAndExitsNonZero()
	{
		// The root GetFiles call (non-recursive) failing persistently must also be caught.
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.FailGetFiles("/device", Com(InvalidData));

		using var target = new TempDirectory();
		var (exitCode, output) = await RunProgramAsync(fake, "download-files", "-n", "MockDevice", "-s", "/device", "-t", target.Path);

		Assert.Equal(1, exitCode);
		Assert.Contains("could not process folder /device", output);
		Assert.Contains("Completed with errors: 0 file(s) and 1 folder(s)", output);
	}

	#endregion

	#region Failed downloads

	[Fact]
	public void Download_BlankNamedEntry_ReturnsFailedWithReason()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device/A");
		fake.AddFile("/device/A/", new byte[] { 1 });

		using var target = new TempDirectory();
		var result = ConnectedMtpDevice(fake).CopyFile(FileCopyMode.Download, "/device/A/", target.Path, skipExisting: true, isMove: false);

		Assert.Equal(FileCopyStatus.Failed, result.CopyStatus);
		Assert.Equal("device object has no name", result.FailureReason);
	}

	[Fact]
	public void Download_BlankNamedEntryWithMove_DoesNotDeleteSource()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device/A");
		fake.AddFile("/device/A/", new byte[] { 1 });

		using var target = new TempDirectory();
		var result = ConnectedMtpDevice(fake).CopyFile(FileCopyMode.Download, "/device/A/", target.Path, skipExisting: true, isMove: true);

		Assert.Equal(FileCopyStatus.Failed, result.CopyStatus);
		Assert.False(result.SourceDeleted);
		Assert.Empty(fake.DeletedFiles);
		Assert.True(fake.FileExists("/device/A/"));
	}

	[Fact]
	public void Download_FailedWithMove_DoesNotDeleteSource()
	{
		// A download that fails on every strategy must leave the source on the device
		var mock = new StrategyTestMock(failureCount: 10);
		mock.Connect();
		mock.AddFolder("/device");
		mock.AddFile("/device/keep.jpg", new byte[] { 1, 2, 3 });
		var device = new MtpDevice(mock);

		using var target = new TempDirectory();
		var result = device.CopyFile(FileCopyMode.Download, "/device/keep.jpg", Path.Combine(target.Path, "keep.jpg"), skipExisting: false, isMove: true);

		Assert.Equal(FileCopyStatus.Failed, result.CopyStatus);
		Assert.False(result.SourceDeleted);
		Assert.True(mock.FileExists("/device/keep.jpg"));
	}

	[Fact]
	public void Download_UnauthorizedAccess_IsNotRetriedAcrossStrategies()
	{
		var mock = new StrategyTestMock(failureCount: 10, exceptionToThrow: new UnauthorizedAccessException("Access to the path is denied."));
		mock.Connect();
		mock.AddFolder("/device");
		mock.AddFile("/device/file.jpg", new byte[] { 1 });
		var device = new MtpDevice(mock);

		using var target = new TempDirectory();
		var result = device.CopyFile(FileCopyMode.Download, "/device/file.jpg", Path.Combine(target.Path, "file.jpg"), skipExisting: false, isMove: false);

		Assert.Equal(FileCopyStatus.Failed, result.CopyStatus);
		Assert.Equal("Access to the path is denied.", result.FailureReason);
		Assert.Single(mock.DownloadAttempts);
	}

	[Fact]
	public async Task Download_BlankNamedEntry_ReportsFailureAndExitsNonZero()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.AddFile("/device/a.jpg", new byte[] { 1, 2, 3 });
		fake.AddFile("/device/", new byte[] { 9 });

		using var target = new TempDirectory();
		var (exitCode, output) = await RunProgramAsync(fake, "download-files", "-n", "MockDevice", "-s", "/device", "-t", target.Path);

		Assert.Equal(1, exitCode);
		Assert.True(File.Exists(Path.Combine(target.Path, "a.jpg")));
		Assert.Contains("FAILED (device object has no name)", output);
		Assert.Contains("failed 1", output);
		Assert.Contains("Completed with errors: 1 file(s) and 0 folder(s)", output);
	}

	[Fact]
	public async Task Download_NoFailures_ExitsZeroWithoutErrorSummary()
	{
		var fake = new FakeTreeDevice();
		fake.AddFolder("/device");
		fake.AddFile("/device/a.jpg", new byte[] { 1, 2, 3 });

		using var target = new TempDirectory();
		var (exitCode, output) = await RunProgramAsync(fake, "download-files", "-n", "MockDevice", "-s", "/device", "-t", target.Path);

		Assert.Equal(0, exitCode);
		Assert.DoesNotContain("Completed with errors", output);
		Assert.DoesNotContain("failed", output);
	}

	#endregion
}
