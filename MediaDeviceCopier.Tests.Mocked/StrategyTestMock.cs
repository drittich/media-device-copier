using MediaDevices;
using MediaDeviceCopier;

namespace MediaDeviceCopier.Tests.Mocked;

/// <summary>
/// Mock device for testing download strategy execution and ordering
/// </summary>
internal class StrategyTestMock : IMediaDevice
{
	private readonly MockMediaDevice _baseMock = new MockMediaDevice();
	private int _downloadAttemptCount = 0;
	private readonly int _failureCount;
	private readonly Exception? _exceptionToThrow;

	public List<string> DownloadAttempts { get; } = new List<string>();

	/// <summary>
	/// Creates a mock that succeeds immediately
	/// </summary>
	public StrategyTestMock()
	{
		_failureCount = 0;
		_exceptionToThrow = null;
	}

	/// <summary>
	/// Creates a mock that fails N times before succeeding
	/// </summary>
	/// <param name="failureCount">Number of download attempts to fail</param>
	/// <param name="exceptionToThrow">Optional specific exception to throw</param>
	public StrategyTestMock(int failureCount, Exception? exceptionToThrow = null)
	{
		_failureCount = failureCount;
		_exceptionToThrow = exceptionToThrow ?? new System.Runtime.InteropServices.COMException("Test COM error", unchecked((int)0x80004005));
	}

	public string FriendlyName => _baseMock.FriendlyName;
	public bool IsConnected => _baseMock.IsConnected;

	public void Connect() => _baseMock.Connect();
	public void Dispose() => _baseMock.Dispose();
	public bool FileExists(string path) => _baseMock.FileExists(path);
	public bool DirectoryExists(string folder) => _baseMock.DirectoryExists(folder);
	public void CreateDirectory(string folder) => _baseMock.CreateDirectory(folder);
	public void DeleteFile(string path) => _baseMock.DeleteFile(path);
	public string[] GetFiles(string folder) => _baseMock.GetFiles(folder);
	public string[] GetDirectories(string folder) => _baseMock.GetDirectories(folder);

	// Helper methods for test setup
	public void AddFolder(string folder) => _baseMock.AddFolder(folder);
	public void AddFile(string path, byte[] content, DateTime? timestamp = null) => _baseMock.AddFile(path, content, timestamp);

	public MediaFileInfo GetFileInfo(string path)
	{
		throw new NotImplementedException("MediaFileInfo not needed for strategy tests");
	}

	public void DownloadFile(string sourceFilePath, string targetFilePath)
	{
		DownloadAttempts.Add($"Attempt_{_downloadAttemptCount}");
		_downloadAttemptCount++;

		if (_downloadAttemptCount <= _failureCount)
		{
			throw _exceptionToThrow!;
		}

		// Success: write a test file
		File.WriteAllText(targetFilePath, $"Downloaded after {_downloadAttemptCount} attempts");
	}

	public void UploadFile(string sourceFilePath, string targetFilePath)
	{
		_baseMock.UploadFile(sourceFilePath, targetFilePath);
	}

	public FileComparisonInfo InternalGetComparisonInfo(string path)
	{
		return _baseMock.InternalGetComparisonInfo(path);
	}
}

/// <summary>
/// Captures invoked strategy names for validation
/// </summary>
internal class StrategyCaptureMock
{
	public List<string> InvokedStrategies { get; } = new List<string>();
	private readonly Dictionary<string, bool> _strategyResults;

	public StrategyCaptureMock(Dictionary<string, bool> strategyResults)
	{
		_strategyResults = strategyResults;
	}

	public bool CaptureStrategy(string name, DownloadStrategyContext context)
	{
		InvokedStrategies.Add(name);
		return _strategyResults.TryGetValue(name, out var result) && result;
	}
}