using System.Runtime.InteropServices;

namespace LicenseSeat.Tests;

/// <summary>
/// Records which runtime ran the suite and what it reports as the OS version,
/// so the .NET Framework 4.8 lane (#26) leaves evidence of the two
/// runtime-dependent inputs: the framework itself and Environment.OSVersion,
/// which feeds the legacy device fingerprint.
/// </summary>
public class RuntimeEvidenceTests(ITestOutputHelper output)
{
    [Fact]
    public void ReportsRuntimeAndFingerprintInputs()
    {
        output.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        output.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        output.WriteLine($"Environment.OSVersion: {Environment.OSVersion}");
        output.WriteLine($"Device fingerprint: {DeviceIdentifier.Generate()}");

#if NETFRAMEWORK
        Assert.StartsWith(".NET Framework 4.8", RuntimeInformation.FrameworkDescription, StringComparison.Ordinal);
#else
        Assert.DoesNotContain(".NET Framework", RuntimeInformation.FrameworkDescription, StringComparison.Ordinal);
#endif
        Assert.Equal(32, DeviceIdentifier.Generate().Length);
    }
}
