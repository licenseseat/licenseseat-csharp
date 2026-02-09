using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LicenseSeat.Tests;

public class TelemetryPayloadTests
{
    /// <summary>
    /// Reset static state before each test to avoid cross-test pollution.
    /// </summary>
    private static void ResetUserOverrides()
    {
        TelemetryPayload.UserAppVersion = null;
        TelemetryPayload.UserAppBuild = null;
    }

    // ================================================================
    // Collect() basic behavior
    // ================================================================

    [Fact]
    public void Collect_ReturnsNonNullPayload()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.NotNull(payload);
    }

    [Fact]
    public void Collect_SdkName_IsCsharp()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.Equal("csharp", payload.SdkName);
    }

    [Fact]
    public void Collect_SdkVersion_MatchesClientConstant()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.Equal(LicenseSeatClient.SdkVersion, payload.SdkVersion);
    }

    // ================================================================
    // os_name — should be "macOS", "Windows", "Linux", or "Unknown"
    // ================================================================

    [Fact]
    public void Collect_OsName_IsKnownValue()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        var valid = new[] { "macOS", "Windows", "Linux", "Unknown" };
        Assert.Contains(payload.OsName, valid);
    }

    [Fact]
    public void Collect_OsName_IsNotEmpty()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.False(string.IsNullOrEmpty(payload.OsName));
    }

    // ================================================================
    // os_version
    // ================================================================

    [Fact]
    public void Collect_OsVersion_IsNotEmpty()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.False(string.IsNullOrEmpty(payload.OsVersion));
    }

    // ================================================================
    // platform — fixed: should be "native" (not duplicate of os_name)
    // ================================================================

    [Fact]
    public void Collect_Platform_ReturnsNative()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        // In a standard .NET test runner, Unity is not present
        Assert.Equal("native", payload.Platform);
    }

    [Fact]
    public void Collect_Platform_DoesNotDuplicateOsName()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        // The old bug had platform == os_name (e.g. "macOS")
        if (payload.OsName != "native")
        {
            Assert.NotEqual(payload.OsName, payload.Platform);
        }
    }

    // ================================================================
    // device_model
    // ================================================================

    [Fact]
    public void Collect_DeviceModel_IsNotNull()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        // Should return Environment.MachineName
        Assert.NotNull(payload.DeviceModel);
        Assert.False(string.IsNullOrEmpty(payload.DeviceModel));
    }

    // ================================================================
    // locale
    // ================================================================

    [Fact]
    public void Collect_Locale_IsNotEmpty()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.False(string.IsNullOrEmpty(payload.Locale));
    }

    // ================================================================
    // timezone — fixed: should be IANA format, not Windows format
    // ================================================================

    [Fact]
    public void Collect_Timezone_IsNotEmpty()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.False(string.IsNullOrEmpty(payload.Timezone));
    }

    [Fact]
    public void Collect_Timezone_DoesNotContainWindowsNames()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        // Windows timezone names contain "Standard Time" or "Daylight Time"
        // IANA names use "/" like "America/New_York"
        // On macOS/Linux the native ID is already IANA, so this should always pass
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.DoesNotContain("Standard Time", payload.Timezone);
            Assert.DoesNotContain("Daylight Time", payload.Timezone);
        }
    }

    // ================================================================
    // app_version — fixed: now populated from entry assembly or user override
    // ================================================================

    [Fact]
    public void Collect_AppVersion_UserOverrideTakesPrecedence()
    {
        try
        {
            TelemetryPayload.UserAppVersion = "3.2.1";
            TelemetryPayload.UserAppBuild = null;

            var payload = TelemetryPayload.Collect();
            Assert.Equal("3.2.1", payload.AppVersion);
        }
        finally
        {
            ResetUserOverrides();
        }
    }

    [Fact]
    public void Collect_AppBuild_UserOverrideTakesPrecedence()
    {
        try
        {
            TelemetryPayload.UserAppVersion = null;
            TelemetryPayload.UserAppBuild = "build-42";

            var payload = TelemetryPayload.Collect();
            Assert.Equal("build-42", payload.AppBuild);
        }
        finally
        {
            ResetUserOverrides();
        }
    }

    [Fact]
    public void Collect_AppVersionAndBuild_BothOverrides()
    {
        try
        {
            TelemetryPayload.UserAppVersion = "1.0.0";
            TelemetryPayload.UserAppBuild = "abc123";

            var payload = TelemetryPayload.Collect();
            Assert.Equal("1.0.0", payload.AppVersion);
            Assert.Equal("abc123", payload.AppBuild);
        }
        finally
        {
            ResetUserOverrides();
        }
    }

    // ================================================================
    // NEW FIELD: device_type
    // ================================================================

    [Fact]
    public void Collect_DeviceType_IsNotNull()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.NotNull(payload.DeviceType);
    }

    [Fact]
    public void Collect_DeviceType_IsKnownValue()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        var valid = new[] { "desktop", "server", "mobile", "unknown" };
        Assert.Contains(payload.DeviceType, valid);
    }

    // ================================================================
    // NEW FIELD: architecture
    // ================================================================

    [Fact]
    public void Collect_Architecture_IsNotNull()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.NotNull(payload.Architecture);
    }

    [Fact]
    public void Collect_Architecture_IsLowercase()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.Equal(payload.Architecture, payload.Architecture!.ToLowerInvariant());
    }

    [Fact]
    public void Collect_Architecture_IsKnownValue()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        // Common architectures
        var valid = new[] { "arm64", "x64", "x86", "arm", "wasm", "s390x", "loongarch64", "armv6", "ppc64le" };
        Assert.Contains(payload.Architecture, valid);
    }

    // ================================================================
    // NEW FIELD: cpu_cores
    // ================================================================

    [Fact]
    public void Collect_CpuCores_IsPositive()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.NotNull(payload.CpuCores);
        Assert.True(payload.CpuCores > 0, $"Expected positive cpu_cores, got {payload.CpuCores}");
    }

    [Fact]
    public void Collect_CpuCores_MatchesEnvironmentProcessorCount()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.Equal(Environment.ProcessorCount, payload.CpuCores);
    }

    // ================================================================
    // NEW FIELD: memory_gb
    // ================================================================

    [Fact]
    public void Collect_MemoryGb_IsPositiveWhenAvailable()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        // On .NET 9 (test runner), GC.GetGCMemoryInfo() should be available
        if (payload.MemoryGb != null)
        {
            Assert.True(payload.MemoryGb > 0, $"Expected positive memory_gb, got {payload.MemoryGb}");
        }
        // If null, the API isn't available (acceptable on some runtimes)
    }

    // ================================================================
    // NEW FIELD: language
    // ================================================================

    [Fact]
    public void Collect_Language_IsNotNull()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.NotNull(payload.Language);
    }

    [Fact]
    public void Collect_Language_IsTwoLetterCode()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.NotNull(payload.Language);
        // ISO 639-1 two-letter codes (some are 3, but TwoLetterISOLanguageName should be 2-3)
        Assert.InRange(payload.Language!.Length, 2, 3);
    }

    // ================================================================
    // NEW FIELD: runtime_version
    // ================================================================

    [Fact]
    public void Collect_RuntimeVersion_IsNotNull()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        Assert.NotNull(payload.RuntimeVersion);
    }

    [Fact]
    public void Collect_RuntimeVersion_ContainsDotNet()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        // RuntimeInformation.FrameworkDescription returns something like ".NET 9.0.1"
        Assert.Contains(".NET", payload.RuntimeVersion!);
    }

    // ================================================================
    // ToDictionary() — all fields present
    // ================================================================

    [Fact]
    public void ToDictionary_ContainsAllRequiredKeys()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        var dict = payload.ToDictionary();

        // Required (always present) keys
        Assert.True(dict.ContainsKey("sdk_name"));
        Assert.Equal("csharp", dict["sdk_name"]);
        Assert.True(dict.ContainsKey("sdk_version"));
        Assert.True(dict.ContainsKey("os_name"));
        Assert.True(dict.ContainsKey("os_version"));
        Assert.True(dict.ContainsKey("platform"));
        Assert.True(dict.ContainsKey("locale"));
        Assert.True(dict.ContainsKey("timezone"));
    }

    [Fact]
    public void ToDictionary_ContainsNewFields()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        var dict = payload.ToDictionary();

        // New fields should be present (they're non-null on standard .NET)
        Assert.True(dict.ContainsKey("device_type"), "Missing device_type");
        Assert.True(dict.ContainsKey("architecture"), "Missing architecture");
        Assert.True(dict.ContainsKey("cpu_cores"), "Missing cpu_cores");
        Assert.True(dict.ContainsKey("language"), "Missing language");
        Assert.True(dict.ContainsKey("runtime_version"), "Missing runtime_version");
        Assert.True(dict.ContainsKey("device_model"), "Missing device_model");
    }

    [Fact]
    public void ToDictionary_OmitsNullValues()
    {
        var payload = new TelemetryPayload
        {
            SdkVersion = "1.0.0",
            OsName = "Test",
            OsVersion = "1.0",
            Platform = "native",
            Locale = "en-US",
            Timezone = "UTC",
            // All nullable fields left as null
            DeviceModel = null,
            AppVersion = null,
            AppBuild = null,
            DeviceType = null,
            Architecture = null,
            CpuCores = null,
            MemoryGb = null,
            Language = null,
            RuntimeVersion = null,
        };

        var dict = payload.ToDictionary();

        Assert.False(dict.ContainsKey("device_model"));
        Assert.False(dict.ContainsKey("app_version"));
        Assert.False(dict.ContainsKey("app_build"));
        Assert.False(dict.ContainsKey("device_type"));
        Assert.False(dict.ContainsKey("architecture"));
        Assert.False(dict.ContainsKey("cpu_cores"));
        Assert.False(dict.ContainsKey("memory_gb"));
        Assert.False(dict.ContainsKey("language"));
        Assert.False(dict.ContainsKey("runtime_version"));
    }

    [Fact]
    public void ToDictionary_IncludesNonNullOptionalFields()
    {
        var payload = new TelemetryPayload
        {
            SdkVersion = "1.0.0",
            OsName = "Test",
            OsVersion = "1.0",
            Platform = "native",
            Locale = "en-US",
            Timezone = "UTC",
            DeviceModel = "TestMachine",
            AppVersion = "2.0.0",
            AppBuild = "build-1",
            DeviceType = "desktop",
            Architecture = "arm64",
            CpuCores = 8,
            MemoryGb = 16,
            Language = "en",
            RuntimeVersion = ".NET 9.0.1",
        };

        var dict = payload.ToDictionary();

        Assert.Equal("TestMachine", dict["device_model"]);
        Assert.Equal("2.0.0", dict["app_version"]);
        Assert.Equal("build-1", dict["app_build"]);
        Assert.Equal("desktop", dict["device_type"]);
        Assert.Equal("arm64", dict["architecture"]);
        Assert.Equal(8, dict["cpu_cores"]);
        Assert.Equal(16, dict["memory_gb"]);
        Assert.Equal("en", dict["language"]);
        Assert.Equal(".NET 9.0.1", dict["runtime_version"]);
    }

    [Fact]
    public void ToDictionary_CpuCores_IsInteger()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        var dict = payload.ToDictionary();

        if (dict.TryGetValue("cpu_cores", out var cpuCores))
        {
            Assert.IsType<int>(cpuCores);
        }
    }

    [Fact]
    public void ToDictionary_MemoryGb_IsInteger()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        var dict = payload.ToDictionary();

        if (dict.TryGetValue("memory_gb", out var memoryGb))
        {
            Assert.IsType<int>(memoryGb);
        }
    }

    // ================================================================
    // Telemetry never throws
    // ================================================================

    [Fact]
    public void Collect_NeverThrows()
    {
        ResetUserOverrides();
        var exception = Record.Exception(() => TelemetryPayload.Collect());
        Assert.Null(exception);
    }

    [Fact]
    public void ToDictionary_NeverThrows()
    {
        ResetUserOverrides();
        var payload = TelemetryPayload.Collect();
        var exception = Record.Exception(() => payload.ToDictionary());
        Assert.Null(exception);
    }

    // ================================================================
    // Options pass AppVersion/AppBuild to telemetry
    // ================================================================

    [Fact]
    public void ClientOptions_AppVersion_PassedToTelemetry()
    {
        try
        {
            var options = new LicenseSeatClientOptions
            {
                ApiKey = "test",
                ProductSlug = "test",
                AutoInitialize = false,
                AutoValidateInterval = TimeSpan.Zero,
                AppVersion = "5.0.0",
                AppBuild = "500",
            };

            // Creating the client sets the static overrides
            using var client = new LicenseSeatClient(options);

            var payload = TelemetryPayload.Collect();
            Assert.Equal("5.0.0", payload.AppVersion);
            Assert.Equal("500", payload.AppBuild);
        }
        finally
        {
            ResetUserOverrides();
        }
    }
}
