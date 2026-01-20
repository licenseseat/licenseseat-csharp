using System;
using System.Security.Cryptography;
using System.Text;

namespace LicenseSeat;

/// <summary>
/// Utility class for generating device identifiers.
/// </summary>
internal static class DeviceIdentifier
{
    /// <summary>
    /// Generates a unique device identifier.
    /// </summary>
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
    /// Generates a device identifier from custom input.
    /// </summary>
    /// <param name="input">The input to hash.</param>
    /// <returns>A deterministic device identifier.</returns>
    public static string FromInput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(input));
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
