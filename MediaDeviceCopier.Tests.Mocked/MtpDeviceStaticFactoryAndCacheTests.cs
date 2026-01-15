using MediaDeviceCopier;
using MediaDevices;
using Xunit;

namespace MediaDeviceCopier.Tests.Mocked;

[Collection(nameof(MtpDeviceStaticStateCollection))]
public sealed class MtpDeviceStaticFactoryAndCacheTests
{
	private sealed class LocalTestMediaDevice : IMediaDevice
	{
		public LocalTestMediaDevice(string friendlyName)
		{
			FriendlyName = friendlyName;
		}

		public bool IsConnected => false;
		public string FriendlyName { get; }

		public void Connect() { }
		public bool FileExists(string path) => throw new NotImplementedException();
		public MediaFileInfo GetFileInfo(string path) => throw new NotImplementedException();
		public void DownloadFile(string sourceFilePath, string targetFilePath) => throw new NotImplementedException();
		public void UploadFile(string sourceFilePath, string targetFilePath) => throw new NotImplementedException();
		public void DeleteFile(string path) => throw new NotImplementedException();
		public string[] GetDirectories(string folder) => throw new NotImplementedException();
		public void CreateDirectory(string folder) => throw new NotImplementedException();
		public bool DirectoryExists(string folder) => throw new NotImplementedException();
		public string[] GetFiles(string folder) => throw new NotImplementedException();
		public void Dispose() { }
	}

	[Fact]
	public void GetAll_CachesDevices_DeviceFactoryInvokedOnlyOnceAcrossRepeatedCalls()
	{
		var originalFactory = MtpDevice.DeviceFactory;
		try
		{
			int factoryCalls = 0;
			MtpDevice.DeviceFactory = () =>
			{
				factoryCalls++;
				return new IMediaDevice[]
				{
					new LocalTestMediaDevice("Device2"),
					new LocalTestMediaDevice("Device1"),
				};
			};

			var first = MtpDevice.GetAll();
			var second = MtpDevice.GetAll();

			Assert.Equal(1, factoryCalls);
			Assert.Same(first, second);
			Assert.Equal(2, first.Count);
		}
		finally
		{
			MtpDevice.DeviceFactory = originalFactory;
		}
	}

	[Fact]
	public void DeviceFactory_Reset_ClearsGetAllCache_AndReturnsDevicesFromNewFactory()
	{
		var originalFactory = MtpDevice.DeviceFactory;
		try
		{
			int factoryCallsA = 0;
			int factoryCallsB = 0;

			MtpDevice.DeviceFactory = () =>
			{
				factoryCallsA++;
				return new IMediaDevice[]
				{
					new LocalTestMediaDevice("A1"),
				};
			};

			var devicesFromA = MtpDevice.GetAll();
			Assert.Equal(new[] { "A1" }, devicesFromA.Select(d => d.FriendlyName).ToArray());
			Assert.Equal(1, factoryCallsA);

			MtpDevice.DeviceFactory = () =>
			{
				factoryCallsB++;
				return new IMediaDevice[]
				{
					new LocalTestMediaDevice("B1"),
				};
			};

			var devicesFromB = MtpDevice.GetAll();
			Assert.Equal(new[] { "B1" }, devicesFromB.Select(d => d.FriendlyName).ToArray());
			Assert.Equal(1, factoryCallsB);
		}
		finally
		{
			MtpDevice.DeviceFactory = originalFactory;
		}
	}

	[Fact]
	public void GetAll_ReturnsDevicesOrderedByFriendlyName_UsingDefaultOrderBySemantics()
	{
		var originalFactory = MtpDevice.DeviceFactory;
		try
		{
			var inputNames = new[] { "b", "A", "a" };
			var expectedNames = inputNames.OrderBy(n => n).ToArray();

			MtpDevice.DeviceFactory = () => inputNames.Select(n => (IMediaDevice)new LocalTestMediaDevice(n));

			var actualNames = MtpDevice.GetAll().Select(d => d.FriendlyName).ToArray();

			Assert.Equal(expectedNames, actualNames);
		}
		finally
		{
			MtpDevice.DeviceFactory = originalFactory;
		}
	}

	[Fact]
	public void GetByName_IsCaseInsensitive()
	{
		var originalFactory = MtpDevice.DeviceFactory;
		try
		{
			MtpDevice.DeviceFactory = () => new IMediaDevice[]
			{
				new LocalTestMediaDevice("MyDevice"),
			};

			Assert.NotNull(MtpDevice.GetByName("mydevice"));
			Assert.NotNull(MtpDevice.GetByName("MYDEVICE"));

			var device = MtpDevice.GetByName("mydevice");
			Assert.NotNull(device);
			Assert.Equal("MyDevice", device!.FriendlyName);
		}
		finally
		{
			MtpDevice.DeviceFactory = originalFactory;
		}
	}
}
