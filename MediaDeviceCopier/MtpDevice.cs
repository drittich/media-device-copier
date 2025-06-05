﻿using MediaDevices;

namespace MediaDeviceCopier
{
	public class MtpDevice : IDisposable
	{
		private bool _disposed;
		private readonly MediaDevice _device;
		public MtpDevice(MediaDevice device)
		{
			_device = device;
		}

		public void Connect()
		{
			if (_device.IsConnected)
			{
				return;
			}

			_device.Connect();
		}

		private static List<MediaDevice>? _listDevices;
		public static List<MediaDevice> GetAll()
		{
			_listDevices ??= MediaDevice.GetDevices()
					.OrderBy(d => d.FriendlyName)
					.ToList();

			return _listDevices;
		}

		public static MtpDevice? GetByName(string deviceName)
		{
			var device = GetAll()
				.FirstOrDefault(d => d.FriendlyName.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase));

			if (device is null)
			{
				return null;
			}

			return new MtpDevice(device);
		}

		public MediaFileInfo GetFileInfo(string filePath)
		{
			if (!_device.FileExists(filePath))
			{
				throw new FileNotFoundException($"File not found: {filePath}");
			}

			return _device.GetFileInfo(filePath);
		}

		public FileCopyResultInfo CopyFile(FileCopyMode fileCopyMode, string sourceFilePath, string targetFilePath, bool skipExisting)
		{
			if (!_device.IsConnected)
			{
				throw new InvalidOperationException("Device is not connected.");
			}

			if (fileCopyMode == FileCopyMode.Download)
			{
				return DownloadFile(sourceFilePath, targetFilePath, skipExisting);
			}
			else if (fileCopyMode == FileCopyMode.Upload)
			{
				return UploadFile(sourceFilePath, targetFilePath, skipExisting);
			}
			else
			{
				throw new NotImplementedException();
			}
		}

		private FileCopyResultInfo DownloadFile(string sourceFilePath, string targetFilePath, bool skipExisting)
		{
			FileCopyStatus fileCopyStatus = FileCopyStatus.Copied;
			FileComparisonInfo? mtpFileComparisonInfo = null;
			FileCopyResultInfo fileCopyInfo;

			if (skipExisting && File.Exists(targetFilePath))
			{
				var sizeAndDatesMatch = GetSizeAndDatesMatch(FileCopyMode.Download, sourceFilePath, targetFilePath, out mtpFileComparisonInfo);
				if (sizeAndDatesMatch)
				{
					fileCopyInfo = new()
					{
						CopyStatus = FileCopyStatus.SkippedBecauseAlreadyExists,
						Length = mtpFileComparisonInfo.Length
					};
					return fileCopyInfo;
				}
				else
				{
					fileCopyStatus = FileCopyStatus.CopiedBecauseDateOrSizeMismatch;
				}
			}

			_device.DownloadFile(sourceFilePath, targetFilePath);

			// set the file date to match the source file
			mtpFileComparisonInfo ??= GetComparisonInfo(_device.GetFileInfo(sourceFilePath));
			File.SetLastWriteTime(targetFilePath, mtpFileComparisonInfo.ModifiedDate);

			fileCopyInfo = new()
			{
				CopyStatus = fileCopyStatus,
				Length = mtpFileComparisonInfo.Length
			};
			return fileCopyInfo;
		}

		private FileCopyResultInfo UploadFile(string sourceFilePath, string targetFilePath, bool skipExisting)
		{
			FileCopyStatus fileCopyStatus = FileCopyStatus.Copied;
			FileComparisonInfo? mtpFileComparisonInfo = null;
			FileCopyResultInfo fileCopyInfo;

			if (skipExisting && _device.FileExists(targetFilePath))
			{
				var sizeAndDatesMatch = GetSizeAndDatesMatch(FileCopyMode.Upload, sourceFilePath, targetFilePath, out mtpFileComparisonInfo);
				if (sizeAndDatesMatch)
				{
					fileCopyInfo = new()
					{
						CopyStatus = FileCopyStatus.SkippedBecauseAlreadyExists,
						Length = mtpFileComparisonInfo.Length
					};
					return fileCopyInfo;
				}
				else
				{
					fileCopyStatus = FileCopyStatus.CopiedBecauseDateOrSizeMismatch;
				}
			}

			_device.UploadFile(sourceFilePath, targetFilePath);

                        // TODO: figure out how to set the file date to match the source file
                        // set the file date to match the source file
                        mtpFileComparisonInfo ??= GetComparisonInfo(_device.GetFileInfo(targetFilePath));
			//File.SetLastWriteTime(targetFilePath, mtpFileComparisonInfo.ModifiedDate);

			fileCopyInfo = new()
			{
				CopyStatus = fileCopyStatus,
				Length = mtpFileComparisonInfo.Length
			};
			return fileCopyInfo;
		}

		private bool GetSizeAndDatesMatch(FileCopyMode fileCopyMode, string sourceFilePath, string targetFilePath, out FileComparisonInfo mtpFileComparisonInfo)
		{
			FileComparisonInfo sourceComparisonInfo;
			FileComparisonInfo targetComparisonInfo;

                        if (fileCopyMode is FileCopyMode.Download)
                        {
                                sourceComparisonInfo = GetComparisonInfo(_device.GetFileInfo(sourceFilePath));
                                mtpFileComparisonInfo = sourceComparisonInfo;
                                targetComparisonInfo = GetComparisonInfo(new FileInfo(targetFilePath));
                                return sourceComparisonInfo.Equals(targetComparisonInfo);
                        }
                        else if (fileCopyMode is FileCopyMode.Upload)
                        {
                                sourceComparisonInfo = GetComparisonInfo(new FileInfo(sourceFilePath));
                                targetComparisonInfo = GetComparisonInfo(_device.GetFileInfo(targetFilePath));
                                mtpFileComparisonInfo = targetComparisonInfo;
                                // Uploaded files do not preserve modified dates, so compare only length
                                return sourceComparisonInfo.Length == targetComparisonInfo.Length;
                        }
                        else
                        {
                                throw new NotImplementedException();
                        }
		}

		public string[] GetFiles(string folder)
		{
			if (!_device.DirectoryExists(folder))
			{
				throw new DirectoryNotFoundException($"Folder not found: {folder}");
			}

			return _device.GetFiles(folder);
		}

		public static FileComparisonInfo GetComparisonInfo(MediaFileInfo mediaFileInfo)
		{
			return new()
			{
				Length = mediaFileInfo.Length,
				ModifiedDate = mediaFileInfo.LastWriteTime!.Value
			};
		}

		public static FileComparisonInfo GetComparisonInfo(FileInfo fileInfo)
		{
			return new()
			{
				Length = (ulong)fileInfo.Length,
				ModifiedDate = fileInfo.LastWriteTime
			};
		}

		public bool DirectoryExists(string folder)
		{
			return _device.DirectoryExists(folder);
		}

		public string FriendlyName => _device.FriendlyName;

		void IDisposable.Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			// Call the base class implementation.
			_device.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
