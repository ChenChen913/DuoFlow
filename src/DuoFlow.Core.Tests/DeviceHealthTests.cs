using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// DD-051: the only thing the app may conclude from a failed Present is
/// "retry the swapchain" (no) or "the device is gone" (yes). Getting this
/// classification wrong is expensive in both directions - a false positive
/// restarts a healthy chain, a false negative leaves a black overlay that
/// never recovers.
/// </summary>
public class DeviceHealthTests
{
    [Theory]
    [InlineData(unchecked((int)0x887A0005))]   // DEVICE_REMOVED
    [InlineData(unchecked((int)0x887A0006))]   // DEVICE_RESET
    [InlineData(unchecked((int)0x887A0020))]   // DRIVER_INTERNAL_ERROR
    public void FatalLose_IsRecognised(int hresult)
        => Assert.True(DeviceHealth.IsFatalLoss(hresult));

    [Theory]
    [InlineData(0)]                              // S_OK
    [InlineData(1)]                              // S_FALSE
    [InlineData(unchecked((int)0x887A0001))]     // DXGI_ERROR_INVALID_CALL - a bug, not a loss
    [InlineData(unchecked((int)0x887A0004))]     // UNSUPPORTED
    [InlineData(unchecked((int)0x8007000E))]     // E_OUTOFMEMORY - transient, retrying is right
    [InlineData(unchecked((int)0x80004005))]     // E_FAIL
    public void EverythingElse_IsNotALoss(int hresult)
        => Assert.False(DeviceHealth.IsFatalLoss(hresult));

    [Fact]
    public void Constants_MatchTheDxgiDocumentation()
    {
        Assert.Equal(unchecked((int)0x887A0005), DeviceHealth.DeviceRemoved);
        Assert.Equal(unchecked((int)0x887A0006), DeviceHealth.DeviceReset);
        Assert.Equal(unchecked((int)0x887A0020), DeviceHealth.DriverInternalError);
    }
}
