using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.1 / M3.6: turning a platform failure into something a person can act on.
/// The rule the tests enforce is "never guess": an unrecognised HRESULT must
/// stay Unknown instead of being forced into a plausible bucket, because the
/// wrong bucket produces confident wrong advice.
/// </summary>
public class CameraHealthTests
{
    [Fact]
    public void NoException_MeansHealthy()
    {
        Assert.Equal(CameraFailure.None, CameraHealth.Classify(null));
    }

    [Theory]
    [InlineData(unchecked((int)0x80070005), CameraFailure.AccessDenied)]   // E_ACCESSDENIED
    [InlineData(unchecked((int)0x8007001B), CameraFailure.Disabled)]       // ERROR_DEVICE_DISABLED
    [InlineData(unchecked((int)0x8007048F), CameraFailure.NoDevice)]       // ERROR_DEVICE_NOT_CONNECTED
    [InlineData(unchecked((int)0x80070490), CameraFailure.NoDevice)]       // ERROR_NOT_FOUND
    [InlineData(unchecked((int)0x800700AA), CameraFailure.InUse)]          // ERROR_BUSY
    [InlineData(unchecked((int)0x80070032), CameraFailure.Unsupported)]    // ERROR_NOT_SUPPORTED
    [InlineData(unchecked((int)0x80004005), CameraFailure.Unknown)]        // E_FAIL - deliberately unmapped
    public void HResults_MapToTheExpectedFailure(int hresult, CameraFailure expected)
    {
        Assert.Equal(expected, CameraHealth.FromHResult(hresult));
    }

    [Fact]
    public void ExceptionTypes_AreClassifiedByType_NotJustHResult()
    {
        Assert.Equal(CameraFailure.AccessDenied, CameraHealth.Classify(new UnauthorizedAccessException()));
        Assert.Equal(CameraFailure.NoDevice, CameraHealth.Classify(new FileNotFoundException()));
        Assert.Equal(CameraFailure.Unsupported, CameraHealth.Classify(new DllNotFoundException()));
    }

    [Fact]
    public void InnerException_IsInspected_WhenTheOuterOneSaysNothing()
    {
        var outer = new InvalidOperationException("wrapper", new UnauthorizedAccessException());
        Assert.Equal(CameraFailure.AccessDenied, CameraHealth.Classify(outer));
    }

    [Fact]
    public void UnknownStaysUnknown_EvenThroughWrapping()
    {
        var outer = new InvalidOperationException("wrapper", new InvalidOperationException("nothing useful"));
        Assert.Equal(CameraFailure.Unknown, CameraHealth.Classify(outer));
    }

    [Fact]
    public void EveryFailure_HasAnActionableSentence()
    {
        foreach (CameraFailure failure in Enum.GetValues<CameraFailure>())
        {
            string text = CameraHealth.Describe(failure);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.NotEqual(failure.ToString(), text);   // a real sentence, not the enum name
        }
    }

    [Theory]
    [InlineData(CameraFailure.None, true)]
    [InlineData(CameraFailure.NoDevice, true)]        // re-enumerated on resume
    [InlineData(CameraFailure.AccessDenied, true)]    // user can flip the privacy switch
    [InlineData(CameraFailure.InUse, true)]           // other app can be closed
    [InlineData(CameraFailure.Disabled, false)]       // needs Device Manager
    [InlineData(CameraFailure.Unsupported, false)]    // hardware limitation
    [InlineData(CameraFailure.Unknown, false)]
    public void Recoverability_DecidesWhetherRetryingIsWorthIt(CameraFailure failure, bool expected)
    {
        Assert.Equal(expected, CameraHealth.IsRecoverable(failure));
    }
}
