using Xunit;

namespace MediaDeviceCopier.Tests.RealDevice;

public class RealDeviceTests
{
    private static MediaDeviceCopier.MtpDevice? GetFirstDevice()
    {
        var device = MediaDeviceCopier.MtpDevice.GetAll().FirstOrDefault();
        return device;
    }

    [Fact]
    public void ListDevices_ReturnsAtLeastZero()
    {
        var devices = MediaDeviceCopier.MtpDevice.GetAll();
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
