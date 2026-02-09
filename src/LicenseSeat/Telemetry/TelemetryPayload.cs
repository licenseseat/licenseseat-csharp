using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LicenseSeat;

/// <summary>
/// Collects device telemetry for SDK API requests.
/// </summary>
internal sealed class TelemetryPayload
{
    public string SdkName { get; set; } = string.Empty;
    public string SdkVersion { get; set; } = string.Empty;
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string? AppVersion { get; set; }
    public string? AppBuild { get; set; }
    public string? DeviceType { get; set; }
    public string? Architecture { get; set; }
    public int? CpuCores { get; set; }
    public int? MemoryGb { get; set; }
    public string? Language { get; set; }
    public string? RuntimeVersion { get; set; }

    /// <summary>
    /// User-provided app version (set via LicenseSeatClientOptions).
    /// </summary>
    internal static string? UserAppVersion { get; set; }

    /// <summary>
    /// User-provided app build (set via LicenseSeatClientOptions).
    /// </summary>
    internal static string? UserAppBuild { get; set; }

    /// <summary>
    /// Collects telemetry from the current environment.
    /// </summary>
    public static TelemetryPayload Collect()
    {
        return new TelemetryPayload
        {
            SdkName = "csharp",
            SdkVersion = LicenseSeatClient.SdkVersion,
            OsName = GetOsName(),
            OsVersion = GetOsVersion(),
            Platform = GetPlatform(),
            DeviceModel = GetDeviceModel(),
            Locale = GetLocale(),
            Timezone = GetTimezone(),
            AppVersion = GetAppVersion(),
            AppBuild = GetAppBuild(),
            DeviceType = GetDeviceType(),
            Architecture = GetArchitecture(),
            CpuCores = GetCpuCores(),
            MemoryGb = GetMemoryGb(),
            Language = GetLanguage(),
            RuntimeVersion = GetRuntimeVersion(),
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
            ["sdk_name"] = SdkName,
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

        if (DeviceType != null)
        {
            dict["device_type"] = DeviceType;
        }

        if (Architecture != null)
        {
            dict["architecture"] = Architecture;
        }

        if (CpuCores != null)
        {
            dict["cpu_cores"] = CpuCores.Value;
        }

        if (MemoryGb != null)
        {
            dict["memory_gb"] = MemoryGb.Value;
        }

        if (Language != null)
        {
            dict["language"] = Language;
        }

        if (RuntimeVersion != null)
        {
            dict["runtime_version"] = RuntimeVersion;
        }

        return dict;
    }

    private static string GetOsName()
    {
        try
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
        }
        catch
        {
            // Telemetry must never throw
        }

        return "Unknown";
    }

    private static string GetOsVersion()
    {
        try
        {
            return Environment.OSVersion.Version.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetPlatform()
    {
        try
        {
            // Check for Unity runtime
            var unityType = Type.GetType("UnityEngine.Application, UnityEngine.CoreModule", throwOnError: false)
                         ?? Type.GetType("UnityEngine.Application, UnityEngine", throwOnError: false);
            if (unityType != null)
            {
                return "unity";
            }
        }
        catch
        {
            // Telemetry must never throw
        }

        return "native";
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

    private static string GetLocale()
    {
        try
        {
            return CultureInfo.CurrentCulture.Name;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetTimezone()
    {
        try
        {
            var tz = TimeZoneInfo.Local;

#if NETSTANDARD2_0
            // On netstandard2.0, TimeZoneInfo.Local.Id returns IANA on
            // macOS/Linux but Windows format on Windows.
            // Use reflection to call TryConvertWindowsIdToIanaId if available (.NET 6+).
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var convertMethod = typeof(TimeZoneInfo).GetMethod(
                    "TryConvertWindowsIdToIanaId",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string).MakeByRefType() },
                    null);
                if (convertMethod != null)
                {
                    var args = new object?[] { tz.Id, null };
                    var result = (bool)convertMethod.Invoke(null, args)!;
                    if (result && args[1] is string ianaId)
                    {
                        return ianaId;
                    }
                }
            }
#else
            // .NET 6+: use HasIanaId / TryConvertWindowsIdToIanaId directly
            if (!tz.HasIanaId)
            {
                if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var ianaId))
                {
                    return ianaId;
                }
            }
#endif

            return tz.Id;
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string? GetAppVersion()
    {
        // Prefer user-provided value
        if (!string.IsNullOrEmpty(UserAppVersion))
        {
            return UserAppVersion;
        }

        try
        {
            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetAppBuild()
    {
        // Prefer user-provided value
        if (!string.IsNullOrEmpty(UserAppBuild))
        {
            return UserAppBuild;
        }

        try
        {
            return Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetDeviceType()
    {
        try
        {
            // Check for Unity runtime first
            var unitySystemInfoType = Type.GetType("UnityEngine.SystemInfo, UnityEngine.CoreModule", throwOnError: false)
                                   ?? Type.GetType("UnityEngine.SystemInfo, UnityEngine", throwOnError: false);
            if (unitySystemInfoType != null)
            {
                var deviceTypeProp = unitySystemInfoType.GetProperty("deviceType", BindingFlags.Public | BindingFlags.Static);
                if (deviceTypeProp != null)
                {
                    var value = deviceTypeProp.GetValue(null);
                    var name = value?.ToString()?.ToLowerInvariant();
                    if (name == "handheld") return "mobile";
                    if (name == "desktop") return "desktop";
                    return name ?? "unknown";
                }
            }

            // Standard .NET: check if interactive
            if (!Environment.UserInteractive)
            {
                return "server";
            }

            return "desktop";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string? GetArchitecture()
    {
        try
        {
            return RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static int? GetCpuCores()
    {
        try
        {
            return Environment.ProcessorCount;
        }
        catch
        {
            return null;
        }
    }

    private static int? GetMemoryGb()
    {
        try
        {
            // GC.GetGCMemoryInfo() is available in .NET Core 3.0+ but not in netstandard2.0.
            // Use reflection to call it safely.
            var method = typeof(GC).GetMethod("GetGCMemoryInfo", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method != null)
            {
                var info = method.Invoke(null, null);
                if (info != null)
                {
                    var totalProp = info.GetType().GetProperty("TotalAvailableMemoryBytes");
                    if (totalProp != null)
                    {
                        var totalBytes = (long)totalProp.GetValue(info)!;
                        var gb = (int)(totalBytes / (1024L * 1024L * 1024L));
                        return gb > 0 ? gb : null;
                    }
                }
            }
        }
        catch
        {
            // Not available on this runtime
        }

        return null;
    }

    private static string? GetLanguage()
    {
        try
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetRuntimeVersion()
    {
        try
        {
            return RuntimeInformation.FrameworkDescription;
        }
        catch
        {
            return null;
        }
    }
}
