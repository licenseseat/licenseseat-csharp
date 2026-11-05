#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace LicenseSeat
{

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
        /// first activated fingerprint is stored and stays authoritative across
        /// OS updates (see <c>LicenseCache</c>).
        /// </remarks>
        /// <returns>A unique device identifier string.</returns>
        public static string Generate()
        {
            // Generate a deterministic ID based on machine characteristics if possible,
            // otherwise fall back to a random GUID stored in memory.

            try
            {
                // Try to create a semi-stable identifier from available machine info
                var machineName = Environment.MachineName;
                var userName = Environment.UserName;
                var osVersion = Environment.OSVersion.ToString();

                var input = $"{machineName}:{userName}:{osVersion}";
                return ComputeHash(input);
            }
            catch
            {
                // Fall back to a random GUID if we can't get machine info
                return Guid.NewGuid().ToString("N");
            }
        }

        /// <summary>
        /// Computes the machine anchor that binds a persisted device fingerprint
        /// to the machine and user account it was activated on.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Generate"/>, the anchor deliberately excludes the
        /// operating-system version, so it survives Windows cumulative and
        /// feature updates. It is only ever compared locally against the anchor
        /// stored next to a persisted fingerprint; it is never sent to the
        /// server and never used as a fingerprint, so introducing it changes no
        /// existing activation.
        /// </remarks>
        /// <returns>The anchor, or <c>null</c> when machine identity is unavailable.</returns>
        public static string? ComputeAnchor()
        {
            try
            {
                var machineName = Environment.MachineName;
                var userName = Environment.UserName;
                if (string.IsNullOrEmpty(machineName) || string.IsNullOrEmpty(userName))
                {
                    return null;
                }

                return ComputeHash($"licenseseat-device-anchor-v1:{machineName}:{userName}");
            }
            catch (Exception)
            {
                return null;
            }
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
