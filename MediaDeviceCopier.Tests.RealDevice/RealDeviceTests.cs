namespace MediaDeviceCopier.Tests.RealDevice;

public class RealDeviceTests
{
	private static MtpDevice? GetFirstDevice()
	{
		var device = MtpDevice.GetAll().FirstOrDefault();
		return device;
	}

	[Fact]
	public void ListDevices_ReturnsAtLeastZero()
	{
		var devices = MtpDevice.GetAll();
		Assert.NotNull(devices);
	}

	[Fact]
	public void Connect_FirstDevice_IfAvailable()
	{
		var device = GetFirstDevice();
		if (device == null)
		{
			return; // no device connected
		}
		device.Connect();
		Assert.True(true);
	}
}
