using MediaDeviceCopier;
using MediaDevices;

namespace MediaDeviceCopier.Tests.Mocked;

internal class MockMediaDevice : IMediaDevice
{
    private readonly Dictionary<string, byte[]> _files = new();
    private readonly HashSet<string> _folders = new();
    public bool IsConnected { get; private set; }
    public string FriendlyName { get; init; } = "MockDevice";

    public void Connect() => IsConnected = true;

    public bool FileExists(string path) => _files.ContainsKey(path);

    public MediaFileInfo GetFileInfo(string path) => throw new NotImplementedException();

    public void DownloadFile(string sourceFilePath, string targetFilePath) => File.WriteAllBytes(targetFilePath, _files[sourceFilePath]);

    public void UploadFile(string sourceFilePath, string targetFilePath) => _files[targetFilePath] = File.ReadAllBytes(sourceFilePath);

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

    public void Dispose() { }
}
