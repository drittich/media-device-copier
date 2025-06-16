namespace MediaDeviceCopier;

using MediaDevices;

internal class MediaDeviceWrapper : IMediaDevice
{
	private readonly MediaDevice _device;

	public MediaDeviceWrapper(MediaDevice device)
	{
		_device = device;
	}

	public bool IsConnected => _device.IsConnected;
	public string FriendlyName => _device.FriendlyName;
	public void Connect() => _device.Connect();
	public bool FileExists(string path) => _device.FileExists(path);
	public MediaFileInfo GetFileInfo(string path) => _device.GetFileInfo(path);
	public void DownloadFile(string sourceFilePath, string targetFilePath) => _device.DownloadFile(sourceFilePath, targetFilePath);
	public void UploadFile(string sourceFilePath, string targetFilePath) => _device.UploadFile(sourceFilePath, targetFilePath);
	public string[] GetDirectories(string folder) => _device.GetDirectories(folder);
	public void CreateDirectory(string folder) => _device.CreateDirectory(folder);
	public bool DirectoryExists(string folder) => _device.DirectoryExists(folder);
	public string[] GetFiles(string folder) => _device.GetFiles(folder);
	public void Dispose() => _device.Dispose();
}
