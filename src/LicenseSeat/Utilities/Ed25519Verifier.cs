#nullable enable
using System;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace LicenseSeat
{

    /// <summary>
    /// Utility class for Ed25519 signature verification.
    /// </summary>
    public static class Ed25519Verifier
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        /// <summary>
        /// Verifies an Ed25519 signature against a canonical JSON string.
        /// This is the primary method used for offline token verification.
        /// </summary>
        /// <param name="publicKeyBase64">The public key in Base64 encoding.</param>
        /// <param name="signatureBase64Url">The signature in Base64URL encoding.</param>
        /// <param name="canonicalJson">The canonical JSON string that was signed.</param>
        /// <returns>True if the signature is valid; otherwise, false.</returns>
        public static bool VerifyCanonical(string publicKeyBase64, string signatureBase64Url, string canonicalJson)
        {
            if (string.IsNullOrEmpty(publicKeyBase64))
            {
                throw new ArgumentException("Public key cannot be null or empty", nameof(publicKeyBase64));
            }

            if (string.IsNullOrEmpty(signatureBase64Url))
            {
                throw new ArgumentException("Signature cannot be null or empty", nameof(signatureBase64Url));
            }

            if (string.IsNullOrEmpty(canonicalJson))
            {
                throw new ArgumentException("Canonical JSON cannot be null or empty", nameof(canonicalJson));
            }

            try
            {
                var publicKeyBytes = DecodePublicKey(publicKeyBase64);
                var signatureBytes = DecodeSignature(signatureBase64Url);
                var messageBytes = GetBoundedMessageBytes(canonicalJson);

                // Create Ed25519 verifier
                var publicKeyParams = new Ed25519PublicKeyParameters(publicKeyBytes, 0);
                var verifier = new Ed25519Signer();
                verifier.Init(false, publicKeyParams);
                verifier.BlockUpdate(messageBytes, 0, messageBytes.Length);

                return verifier.VerifySignature(signatureBytes);
            }
            catch (CryptoException)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is FormatException ||
                ex is ArgumentException ||
                ex is InvalidOperationException ||
                ex is JsonException ||
                ex is EncoderFallbackException)
            {
                throw new CryptoException(
                    "Ed25519 verification failed because the input was invalid.",
                    CryptoException.VerificationFailedCode,
                    ex);
            }
        }

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
                var publicKeyBytes = DecodePublicKey(publicKeyBase64);
                var signatureBytes = DecodeSignature(signatureBase64Url);

                // Serialize payload to JSON with consistent settings
                var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                var payloadBytes = GetBoundedMessageBytes(payloadJson);

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
            catch (Exception ex) when (
                ex is FormatException ||
                ex is ArgumentException ||
                ex is InvalidOperationException ||
                ex is JsonException ||
                ex is EncoderFallbackException)
            {
                throw new CryptoException(
                    "Ed25519 verification failed because the input was invalid.",
                    CryptoException.VerificationFailedCode,
                    ex);
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
                if (message.Length > SecurityValidation.MaxCanonicalJsonBytes)
                {
                    throw new CryptoException(
                        "Message exceeds the supported verification limit.",
                        CryptoException.VerificationErrorCode);
                }

                var publicKeyBytes = DecodePublicKey(publicKeyBase64);
                var signatureBytes = DecodeSignature(signatureBase64Url);

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
            catch (Exception ex) when (
                ex is FormatException ||
                ex is ArgumentException ||
                ex is InvalidOperationException)
            {
                throw new CryptoException(
                    "Ed25519 verification failed because the input was invalid.",
                    CryptoException.VerificationFailedCode,
                    ex);
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

            var result = a.Length ^ b.Length;
            var maxLength = Math.Max(a.Length, b.Length);
            for (var i = 0; i < maxLength; i++)
            {
                var left = i < a.Length ? a[i] : 0;
                var right = i < b.Length ? b[i] : 0;
                result |= left ^ right;
            }

            return result == 0;
        }

        /// <summary>
        /// Decodes a Base64URL string to bytes.
        /// </summary>
        private static byte[] DecodePublicKey(string input)
        {
            if (!SecurityValidation.IsAsciiPrintable(input, 44, 44))
            {
                throw new CryptoException("Public key must be canonical Base64 for 32 bytes.", CryptoException.InvalidKeyCode);
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(input);
            }
            catch (FormatException ex)
            {
                throw new CryptoException("Public key is not valid Base64.", CryptoException.InvalidKeyCode, ex);
            }

            if (bytes.Length != 32 || !string.Equals(Convert.ToBase64String(bytes), input, StringComparison.Ordinal))
            {
                throw new CryptoException("Public key must be canonical Base64 for 32 bytes.", CryptoException.InvalidKeyCode);
            }

            return bytes;
        }

        private static byte[] DecodeSignature(string input)
        {
            if (!SecurityValidation.IsAsciiPrintable(input, 86, 88))
            {
                throw new CryptoException("Signature is not valid bounded Base64.", CryptoException.InvalidSignatureCode);
            }

            foreach (var character in input)
            {
                var valid = (character >= 'A' && character <= 'Z') ||
                            (character >= 'a' && character <= 'z') ||
                            (character >= '0' && character <= '9') ||
                            character == '+' || character == '/' || character == '-' || character == '_' || character == '=';
                if (!valid)
                {
                    throw new CryptoException("Signature is not valid Base64.", CryptoException.InvalidSignatureCode);
                }
            }

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
                case 1:
                    throw new CryptoException("Signature is not valid Base64.", CryptoException.InvalidSignatureCode);
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(output);
            }
            catch (FormatException ex)
            {
                throw new CryptoException("Signature is not valid Base64.", CryptoException.InvalidSignatureCode, ex);
            }

            if (bytes.Length != 64)
            {
                throw new CryptoException("Signature must decode to 64 bytes.", CryptoException.InvalidSignatureCode);
            }

            var canonical = Convert.ToBase64String(bytes).TrimEnd('=');
            if (!string.Equals(canonical, output.TrimEnd('='), StringComparison.Ordinal))
            {
                throw new CryptoException("Signature is not canonical Base64.", CryptoException.InvalidSignatureCode);
            }

            return bytes;
        }

        private static byte[] GetBoundedMessageBytes(string message)
        {
            if (!SecurityValidation.IsSafeText(message, 1, SecurityValidation.MaxCanonicalJsonBytes))
            {
                throw new CryptoException(
                    "Message contains invalid text or exceeds the supported verification limit.",
                    CryptoException.VerificationErrorCode);
            }

            return StrictUtf8.GetBytes(message);
        }
    }
}
