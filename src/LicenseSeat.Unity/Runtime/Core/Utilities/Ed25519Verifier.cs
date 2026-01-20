using System;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace LicenseSeat;

/// <summary>
/// Utility class for Ed25519 signature verification.
/// </summary>
internal static class Ed25519Verifier
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    /// <summary>
    /// Verifies an Ed25519 signature.
    /// </summary>
    /// <param name="publicKeyBase64">The public key in Base64 encoding.</param>
    /// <param name="signatureBase64Url">The signature in Base64URL encoding.</param>
    /// <param name="payload">The payload object to verify.</param>
    /// <returns>True if the signature is valid; otherwise, false.</returns>
    public static bool Verify(string publicKeyBase64, string signatureBase64Url, object payload)
    {
        if (string.IsNullOrEmpty(publicKeyBase64))
        {
            throw new ArgumentException("Public key cannot be null or empty", nameof(publicKeyBase64));
        }

        if (string.IsNullOrEmpty(signatureBase64Url))
        {
            throw new ArgumentException("Signature cannot be null or empty", nameof(signatureBase64Url));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        try
        {
            // Decode public key from Base64
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            if (publicKeyBytes.Length != 32)
            {
                throw new CryptoException(
                    $"Invalid public key length: {publicKeyBytes.Length} bytes (expected 32)",
                    CryptoException.InvalidKeyCode);
            }

            // Decode signature from Base64URL
            var signatureBytes = Base64UrlDecode(signatureBase64Url);
            if (signatureBytes.Length != 64)
            {
                throw new CryptoException(
                    $"Invalid signature length: {signatureBytes.Length} bytes (expected 64)",
                    CryptoException.InvalidSignatureCode);
            }

            // Serialize payload to JSON with consistent settings
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

            // Create Ed25519 verifier
            var publicKeyParams = new Ed25519PublicKeyParameters(publicKeyBytes, 0);
            var verifier = new Ed25519Signer();
            verifier.Init(false, publicKeyParams);
            verifier.BlockUpdate(payloadBytes, 0, payloadBytes.Length);

            return verifier.VerifySignature(signatureBytes);
        }
        catch (CryptoException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CryptoException($"Ed25519 verification failed: {ex.Message}", CryptoException.VerificationFailedCode);
        }
    }

    /// <summary>
    /// Verifies an Ed25519 signature against raw message bytes.
    /// </summary>
    /// <param name="publicKeyBase64">The public key in Base64 encoding.</param>
    /// <param name="signatureBase64Url">The signature in Base64URL encoding.</param>
    /// <param name="message">The message bytes to verify.</param>
    /// <returns>True if the signature is valid; otherwise, false.</returns>
    public static bool VerifyBytes(string publicKeyBase64, string signatureBase64Url, byte[] message)
    {
        if (string.IsNullOrEmpty(publicKeyBase64))
        {
            throw new ArgumentException("Public key cannot be null or empty", nameof(publicKeyBase64));
        }

        if (string.IsNullOrEmpty(signatureBase64Url))
        {
            throw new ArgumentException("Signature cannot be null or empty", nameof(signatureBase64Url));
        }

        if (message == null || message.Length == 0)
        {
            throw new ArgumentException("Message cannot be null or empty", nameof(message));
        }

        try
        {
            // Decode public key from Base64
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            if (publicKeyBytes.Length != 32)
            {
                throw new CryptoException(
                    $"Invalid public key length: {publicKeyBytes.Length} bytes (expected 32)",
                    CryptoException.InvalidKeyCode);
            }

            // Decode signature from Base64URL
            var signatureBytes = Base64UrlDecode(signatureBase64Url);
            if (signatureBytes.Length != 64)
            {
                throw new CryptoException(
                    $"Invalid signature length: {signatureBytes.Length} bytes (expected 64)",
                    CryptoException.InvalidSignatureCode);
            }

            // Create Ed25519 verifier
            var publicKeyParams = new Ed25519PublicKeyParameters(publicKeyBytes, 0);
            var verifier = new Ed25519Signer();
            verifier.Init(false, publicKeyParams);
            verifier.BlockUpdate(message, 0, message.Length);

            return verifier.VerifySignature(signatureBytes);
        }
        catch (CryptoException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CryptoException($"Ed25519 verification failed: {ex.Message}", CryptoException.VerificationFailedCode);
        }
    }

    /// <summary>
    /// Performs a constant-time comparison of two strings to prevent timing attacks.
    /// </summary>
    /// <param name="a">The first string.</param>
    /// <param name="b">The second string.</param>
    /// <returns>True if the strings are equal; otherwise, false.</returns>
    public static bool ConstantTimeEquals(string? a, string? b)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Decodes a Base64URL string to bytes.
    /// </summary>
    private static byte[] Base64UrlDecode(string input)
    {
        // Convert Base64URL to standard Base64
        var output = input
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding if necessary
        switch (output.Length % 4)
        {
            case 2:
                output += "==";
                break;
            case 3:
                output += "=";
                break;
        }

        return Convert.FromBase64String(output);
    }
}
