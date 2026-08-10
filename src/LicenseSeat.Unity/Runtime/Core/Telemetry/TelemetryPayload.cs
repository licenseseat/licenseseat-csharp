#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LicenseSeat
{

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
        /// Collects telemetry from the current environment.
        /// </summary>
        public static TelemetryPayload Collect(string? appVersion = null, string? appBuild = null)
        {
            return new TelemetryPayload
            {
                SdkName = "csharp",
                SdkVersion = LicenseSeatClient.SdkVersion,
                OsName = Sanitize(GetOsName()) ?? "Unknown",
                OsVersion = Sanitize(GetOsVersion()) ?? "Unknown",
                Platform = Sanitize(GetPlatform()) ?? "native",
                DeviceModel = Sanitize(GetDeviceModel()),
                Locale = Sanitize(GetLocale()) ?? "Unknown",
                Timezone = Sanitize(GetTimezone()) ?? "Unknown",
                AppVersion = Sanitize(GetAppVersion(appVersion)),
                AppBuild = Sanitize(GetAppBuild(appBuild)),
                DeviceType = Sanitize(GetDeviceType()),
                Architecture = Sanitize(GetArchitecture()),
                CpuCores = GetCpuCores(),
                MemoryGb = GetMemoryGb(),
                Language = Sanitize(GetLanguage()),
                RuntimeVersion = Sanitize(GetRuntimeVersion()),
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
                ["sdk_name"] = Sanitize(SdkName) ?? "csharp",
                ["sdk_version"] = Sanitize(SdkVersion) ?? LicenseSeatClient.SdkVersion,
                ["os_name"] = Sanitize(OsName) ?? "Unknown",
                ["os_version"] = Sanitize(OsVersion) ?? "Unknown",
                ["platform"] = Sanitize(Platform) ?? "native",
                ["locale"] = Sanitize(Locale) ?? "Unknown",
                ["timezone"] = Sanitize(Timezone) ?? "Unknown",
            };

            if (Sanitize(DeviceModel) is string deviceModel)
            {
                dict["device_model"] = deviceModel;
            }

            if (Sanitize(AppVersion) is string appVersion)
            {
                dict["app_version"] = appVersion;
            }

            if (Sanitize(AppBuild) is string appBuild)
            {
                dict["app_build"] = appBuild;
            }

            if (Sanitize(DeviceType) is string deviceType)
            {
                dict["device_type"] = deviceType;
            }

            if (Sanitize(Architecture) is string architecture)
            {
                dict["architecture"] = architecture;
            }

            if (CpuCores != null)
            {
                dict["cpu_cores"] = CpuCores.Value;
            }

            if (MemoryGb != null)
            {
                dict["memory_gb"] = MemoryGb.Value;
            }

            if (Sanitize(Language) is string language)
            {
                dict["language"] = language;
            }

            if (Sanitize(RuntimeVersion) is string runtimeVersion)
            {
                dict["runtime_version"] = runtimeVersion;
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
                var name = CultureInfo.CurrentCulture.Name;
                return string.IsNullOrEmpty(name) ? "Unknown" : name;
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

                // Unity and netstandard targets do not expose consistent compile-time
                // symbols for the newer timezone APIs. Probe at runtime so this source
                // stays compatible with Unity's .NET Standard 2.1 profile.
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

                return tz.Id;
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string? GetAppVersion(string? configuredValue)
        {
            // Prefer user-provided value
            if (!string.IsNullOrEmpty(configuredValue))
            {
                return configuredValue;
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

        private static string? GetAppBuild(string? configuredValue)
        {
            // Prefer user-provided value
            if (!string.IsNullOrEmpty(configuredValue))
            {
                return configuredValue;
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

        private static string? Sanitize(string? value)
        {
            return SecurityValidation.IsSafeText(value, 1, 256) ? value : null;
        }
    }
}
