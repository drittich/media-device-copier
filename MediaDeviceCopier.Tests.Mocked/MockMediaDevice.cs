using MediaDevices;
using System.Reflection;
using MediaDeviceCopier;

namespace MediaDeviceCopier.Tests.Mocked;

internal class MockMediaDevice : IMediaDevice
{
	private readonly Dictionary<string, byte[]> _files = new();
	private readonly Dictionary<string, DateTime?> _fileTimestamps = new();
	private readonly HashSet<string> _folders = new();
	public bool IsConnected { get; private set; }
	public string FriendlyName { get; init; } = "MockDevice";

	public void Connect() => IsConnected = true;

	public bool ThrowOnDelete { get; set; }

	public bool FileExists(string path) => _files.ContainsKey(path);

	public MediaFileInfo GetFileInfo(string path)
	{
		if (!_files.ContainsKey(path))
		{
			throw new FileNotFoundException();
		}

		// For testing purposes, we'll use a simpler approach
		// We can't easily mock MediaFileInfo, so let's just throw the expected exception
		// when we have an invalid timestamp to test our fix
		var timestamp = _fileTimestamps.ContainsKey(path) ? _fileTimestamps[path] : DateTime.Now;
		
		// If we have a very old timestamp (invalid for Win32), this will simulate the issue
		if (timestamp.HasValue && timestamp.Value.Year < 1601)
		{
			// This will create a MediaFileInfo that will cause the issue we're trying to fix
			// We need to find another way to test this... let me just test the validation method directly
			throw new NotSupportedException("Invalid timestamp for testing");
		}
		
		// Return a normal MediaFileInfo for valid timestamps
		// We'll need to find the actual MediaFileInfo from the library somehow
		throw new NotImplementedException("Cannot create MediaFileInfo in mock without proper constructor");
	}

	public void DownloadFile(string sourceFilePath, string targetFilePath) => File.WriteAllBytes(targetFilePath, _files[sourceFilePath]);

	public void UploadFile(string sourceFilePath, string targetFilePath) => _files[targetFilePath] = File.ReadAllBytes(sourceFilePath);

	public void DeleteFile(string path)
	{
		if (ThrowOnDelete)
		{
			throw new IOException("Simulated delete failure");
		}
		_files.Remove(path);
		_fileTimestamps.Remove(path);
	}

	public string[] GetDirectories(string folder) => throw new NotImplementedException();
	public void CreateDirectory(string folder) => throw new NotImplementedException();
	public bool DirectoryExists(string folder) => _folders.Contains(folder);

	public string[] GetFiles(string folder)
	{
		if (!DirectoryExists(folder))
		{
			throw new DirectoryNotFoundException();
		}

		return _files.Keys.ToArray();
	}

	public void AddFolder(string folder) => _folders.Add(folder);

	public void AddFile(string path, byte[] content, DateTime? timestamp = null)
	{
		_files[path] = content;
		if (timestamp.HasValue)
		{
			_fileTimestamps[path] = timestamp.Value;
		}
	}

	public FileComparisonInfo InternalGetComparisonInfo(string path)
	{
		if (!_files.ContainsKey(path))
		{
			throw new FileNotFoundException();
		}
		var length = (ulong)_files[path].Length;
		DateTime timestamp;
		if (_fileTimestamps.TryGetValue(path, out var ts) && ts.HasValue)
		{
			timestamp = ts.Value;
		}
		else
		{
			timestamp = DateTime.Now;
		}
		return new FileComparisonInfo
		{
			Length = length,
			ModifiedDate = timestamp
		};
	}

	public void Dispose() { }
}
