namespace MediaDeviceCopier.Tests.Mocked;

public class MtpDeviceMockTests
{
    [Fact]
    public void GetFiles_InvalidFolder_Throws()
    {
        var device = new MtpDevice(new MockMediaDevice());
        Assert.Throws<DirectoryNotFoundException>(() => device.GetFiles("invalid"));
    }

    [Fact]
    public void DeviceFactory_ReturnsMock()
    {
        MtpDevice.DeviceFactory = () => new[] { (IMediaDevice)new MockMediaDevice() };
        var devices = MtpDevice.GetAll();
        Assert.Single(devices);
        Assert.Equal("MockDevice", devices[0].FriendlyName);
    }
}
