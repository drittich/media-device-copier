namespace MediaDeviceCopier;

using MediaDevices;

public interface IMediaDevice : IDisposable
{
    bool IsConnected { get; }
    string FriendlyName { get; }
    void Connect();
    bool FileExists(string path);
    MediaFileInfo GetFileInfo(string path);
    void DownloadFile(string sourceFilePath, string targetFilePath);
    void UploadFile(string sourceFilePath, string targetFilePath);
    public string[] GetDirectories(string folder);
    public void CreateDirectory(string folder);
    bool DirectoryExists(string folder);
    string[] GetFiles(string folder);
}
