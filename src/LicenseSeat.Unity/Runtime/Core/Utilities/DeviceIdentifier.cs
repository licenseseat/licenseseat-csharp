#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace LicenseSeat
{

    /// <summary>
    /// The machine facts the default device fingerprint is derived from.
    /// </summary>
    internal sealed class DeviceInputs
    {
        public DeviceInputs(string machineName, string userName, string osVersion)
        {
            MachineName = machineName;
            UserName = userName;
            OsVersion = osVersion;
        }

        public string MachineName { get; }

        public string UserName { get; }

        public string OsVersion { get; }
    }

    /// <summary>
    /// Utility class for generating device identifiers.
    /// </summary>
    internal static class DeviceIdentifier
    {
        /// <summary>
        /// Generates a unique device identifier.
        /// </summary>
        /// <remarks>
        /// The output of this method is the fingerprint every existing
        /// activation was created with. It must never change: a different value
        /// for the same machine is a new device to the server and consumes a
        /// seat. Its known weakness, that the OS version (including the build
        /// number) is part of the input, is handled by persistence instead: the
        /// first activated fingerprint is stored together with the OS version it
        /// was derived from, and is re-verified against this machine's name and
        /// user name on load (see <c>LicenseCache</c>).
        /// </remarks>
        /// <returns>A unique device identifier string.</returns>
        public static string Generate()
        {
            var inputs = CurrentInputs();
            return inputs == null
                ? Guid.NewGuid().ToString("N") // Machine info unavailable.
                : FromInputs(inputs.MachineName, inputs.UserName, inputs.OsVersion);
        }

        /// <summary>Reads the current machine facts, or <c>null</c> when they are unavailable.</summary>
        public static DeviceInputs? CurrentInputs()
        {
            try
            {
                return new DeviceInputs(
                    Environment.MachineName,
                    Environment.UserName,
                    Environment.OSVersion.ToString());
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The legacy derivation, byte-identical to SDK 0.5.x:
        /// <c>sha256("{machine}:{user}:{osVersion}")</c>, first 16 bytes, lowercase hex.
        /// </summary>
        public static string FromInputs(string machineName, string userName, string osVersion)
        {
            return ComputeHash($"{machineName}:{userName}:{osVersion}");
        }

        /// <summary>
        /// Generates a device identifier from custom input.
        /// </summary>
        /// <param name="input">The input to hash.</param>
        /// <returns>A deterministic device identifier.</returns>
        public static string FromInput(string input)
        {
            if (!SecurityValidation.IsSafeText(input, 1, 4096))
            {
                throw new ArgumentException("Input must be valid text no longer than 4096 bytes.", nameof(input));
            }

            return ComputeHash(input);
        }

        private static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha256.ComputeHash(inputBytes);

            // Return first 32 characters of hex string (16 bytes)
            var sb = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
            {
                sb.Append(hashBytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}
