using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace LicenseSeat;

/// <summary>
/// Collects device telemetry for SDK API requests.
/// </summary>
internal sealed class TelemetryPayload
{
    public string SdkVersion { get; set; } = string.Empty;
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string? AppVersion { get; set; }
    public string? AppBuild { get; set; }

    /// <summary>
    /// Collects telemetry from the current environment.
    /// </summary>
    public static TelemetryPayload Collect()
    {
        return new TelemetryPayload
        {
            SdkVersion = LicenseSeatClient.SdkVersion,
            OsName = GetOsName(),
            OsVersion = Environment.OSVersion.Version.ToString(),
            Platform = GetPlatform(),
            DeviceModel = GetDeviceModel(),
            Locale = CultureInfo.CurrentCulture.Name,
            Timezone = TimeZoneInfo.Local.Id,
        };
    }

    /// <summary>
    /// Converts the telemetry to a dictionary for JSON serialization.
    /// Null values are excluded.
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["sdk_version"] = SdkVersion,
            ["os_name"] = OsName,
            ["os_version"] = OsVersion,
            ["platform"] = Platform,
            ["locale"] = Locale,
            ["timezone"] = Timezone,
        };

        if (DeviceModel != null)
        {
            dict["device_model"] = DeviceModel;
        }

        if (AppVersion != null)
        {
            dict["app_version"] = AppVersion;
        }

        if (AppBuild != null)
        {
            dict["app_build"] = AppBuild;
        }

        return dict;
    }

    private static string GetOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        return "Unknown";
    }

    private static string GetPlatform()
    {
        return GetOsName();
    }

    private static string? GetDeviceModel()
    {
        try
        {
            return Environment.MachineName;
        }
        catch
        {
            return null;
        }
    }
}
