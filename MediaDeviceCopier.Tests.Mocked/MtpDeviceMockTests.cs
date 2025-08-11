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

	[Theory]
	[InlineData("1500-01-01", false)] // Before Win32 epoch
	[InlineData("1600-12-31", false)] // Just before Win32 epoch
	[InlineData("1601-01-01", true)]  // Win32 epoch
	[InlineData("2023-01-01", true)]  // Normal date
	[InlineData("9999-12-31", true)]  // Maximum reasonable date
	public void IsValidWin32FileTime_ValidatesCorrectly(string dateString, bool expectedValid)
	{
		// Arrange
		var testDate = DateTime.Parse(dateString);

		// Act
		var result = MtpDevice.IsValidWin32FileTime(testDate);

		// Assert
		Assert.Equal(expectedValid, result);
	}

	[Fact]
	public void IsValidWin32FileTime_DefaultDateTime_IsInvalid()
	{
		// Arrange
		var defaultDateTime = default(DateTime); // This is January 1, 0001

		// Act
		var result = MtpDevice.IsValidWin32FileTime(defaultDateTime);

		// Assert
		Assert.False(result, "Default DateTime should be invalid for Win32 FileTime");
	}
}
