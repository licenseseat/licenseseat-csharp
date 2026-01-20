using System;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace LicenseSeat.Tests;

public class Ed25519VerifierTests
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    // Helper method to generate test key pair
    private static (string PublicKeyBase64, string PrivateKeyBase64) GenerateTestKeyPair()
    {
        var keyPairGenerator = new Ed25519KeyPairGenerator();
        keyPairGenerator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = keyPairGenerator.GenerateKeyPair();

        var publicKey = (Ed25519PublicKeyParameters)keyPair.Public;
        var privateKey = (Ed25519PrivateKeyParameters)keyPair.Private;

        return (
            Convert.ToBase64String(publicKey.GetEncoded()),
            Convert.ToBase64String(privateKey.GetEncoded())
        );
    }

    // Helper method to sign data
    private static string Sign(string privateKeyBase64, byte[] message)
    {
        var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
        var privateKey = new Ed25519PrivateKeyParameters(privateKeyBytes, 0);

        var signer = new Ed25519Signer();
        signer.Init(true, privateKey);
        signer.BlockUpdate(message, 0, message.Length);
        var signature = signer.GenerateSignature();

        // Convert to Base64URL
        return Convert.ToBase64String(signature)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    [Fact]
    public void Verify_WithValidSignature_ReturnsTrue()
    {
        var (publicKey, privateKey) = GenerateTestKeyPair();
        var payload = new TestPayload { LicenseKey = "TEST-KEY", Status = "active" };

        // Serialize payload the same way Ed25519Verifier does
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, JsonOptions);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var signature = Sign(privateKey, payloadBytes);

        var result = Ed25519Verifier.Verify(publicKey, signature, payload);

        Assert.True(result);
    }

    [Fact]
    public void Verify_WithInvalidSignature_ReturnsFalse()
    {
        var (publicKey, _) = GenerateTestKeyPair();
        var (_, otherPrivateKey) = GenerateTestKeyPair(); // Different key pair
        var payload = new TestPayload { LicenseKey = "TEST-KEY", Status = "active" };

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, JsonOptions);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        // Sign with different key
        var signature = Sign(otherPrivateKey, payloadBytes);

        var result = Ed25519Verifier.Verify(publicKey, signature, payload);

        Assert.False(result);
    }

    [Fact]
    public void Verify_WithTamperedPayload_ReturnsFalse()
    {
        var (publicKey, privateKey) = GenerateTestKeyPair();
        var originalPayload = new TestPayload { LicenseKey = "TEST-KEY", Status = "active" };

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(originalPayload, JsonOptions);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var signature = Sign(privateKey, payloadBytes);

        // Tamper with the payload
        var tamperedPayload = new TestPayload { LicenseKey = "TAMPERED-KEY", Status = "active" };

        var result = Ed25519Verifier.Verify(publicKey, signature, tamperedPayload);

        Assert.False(result);
    }

    [Fact]
    public void Verify_WithNullPublicKey_ThrowsArgumentException()
    {
        var payload = new TestPayload { LicenseKey = "TEST" };

        Assert.Throws<ArgumentException>(() => Ed25519Verifier.Verify(null!, "signature", payload));
    }

    [Fact]
    public void Verify_WithEmptyPublicKey_ThrowsArgumentException()
    {
        var payload = new TestPayload { LicenseKey = "TEST" };

        Assert.Throws<ArgumentException>(() => Ed25519Verifier.Verify("", "signature", payload));
    }

    [Fact]
    public void Verify_WithNullSignature_ThrowsArgumentException()
    {
        var (publicKey, _) = GenerateTestKeyPair();
        var payload = new TestPayload { LicenseKey = "TEST" };

        Assert.Throws<ArgumentException>(() => Ed25519Verifier.Verify(publicKey, null!, payload));
    }

    [Fact]
    public void Verify_WithNullPayload_ThrowsArgumentNullException()
    {
        var (publicKey, _) = GenerateTestKeyPair();

        Assert.Throws<ArgumentNullException>(() => Ed25519Verifier.Verify(publicKey, "signature", null!));
    }

    [Fact]
    public void Verify_WithInvalidPublicKeyLength_ThrowsCryptoException()
    {
        var payload = new TestPayload { LicenseKey = "TEST" };
        var invalidPublicKey = Convert.ToBase64String(new byte[16]); // Wrong length

        var ex = Assert.Throws<CryptoException>(() => Ed25519Verifier.Verify(invalidPublicKey, "AAAA", payload));
        Assert.Equal(CryptoException.InvalidKeyCode, ex.ErrorCode);
    }

    [Fact]
    public void Verify_WithInvalidSignatureLength_ThrowsCryptoException()
    {
        var (publicKey, _) = GenerateTestKeyPair();
        var payload = new TestPayload { LicenseKey = "TEST" };
        var invalidSignature = Convert.ToBase64String(new byte[32]); // Wrong length (should be 64)

        var ex = Assert.Throws<CryptoException>(() => Ed25519Verifier.Verify(publicKey, invalidSignature, payload));
        Assert.Equal(CryptoException.InvalidSignatureCode, ex.ErrorCode);
    }

    [Fact]
    public void VerifyBytes_WithValidSignature_ReturnsTrue()
    {
        var (publicKey, privateKey) = GenerateTestKeyPair();
        var message = Encoding.UTF8.GetBytes("Hello, World!");

        var signature = Sign(privateKey, message);

        var result = Ed25519Verifier.VerifyBytes(publicKey, signature, message);

        Assert.True(result);
    }

    [Fact]
    public void VerifyBytes_WithInvalidSignature_ReturnsFalse()
    {
        var (publicKey, _) = GenerateTestKeyPair();
        var (_, otherPrivateKey) = GenerateTestKeyPair();
        var message = Encoding.UTF8.GetBytes("Hello, World!");

        var signature = Sign(otherPrivateKey, message);

        var result = Ed25519Verifier.VerifyBytes(publicKey, signature, message);

        Assert.False(result);
    }

    [Fact]
    public void VerifyBytes_WithTamperedMessage_ReturnsFalse()
    {
        var (publicKey, privateKey) = GenerateTestKeyPair();
        var originalMessage = Encoding.UTF8.GetBytes("Hello, World!");

        var signature = Sign(privateKey, originalMessage);

        var tamperedMessage = Encoding.UTF8.GetBytes("Hello, World? (tampered)");

        var result = Ed25519Verifier.VerifyBytes(publicKey, signature, tamperedMessage);

        Assert.False(result);
    }

    [Fact]
    public void VerifyBytes_WithEmptyMessage_ThrowsArgumentException()
    {
        var (publicKey, _) = GenerateTestKeyPair();

        Assert.Throws<ArgumentException>(() => Ed25519Verifier.VerifyBytes(publicKey, "signature", Array.Empty<byte>()));
    }

    private sealed class TestPayload
    {
        public string? LicenseKey { get; set; }
        public string? Status { get; set; }
    }

    // ConstantTimeEquals tests
    [Fact]
    public void ConstantTimeEquals_WithEqualStrings_ReturnsTrue()
    {
        Assert.True(Ed25519Verifier.ConstantTimeEquals("test-key", "test-key"));
    }

    [Fact]
    public void ConstantTimeEquals_WithDifferentStrings_ReturnsFalse()
    {
        Assert.False(Ed25519Verifier.ConstantTimeEquals("test-key-1", "test-key-2"));
    }

    [Fact]
    public void ConstantTimeEquals_WithDifferentLengths_ReturnsFalse()
    {
        Assert.False(Ed25519Verifier.ConstantTimeEquals("short", "much-longer-string"));
    }

    [Fact]
    public void ConstantTimeEquals_WithBothNull_ReturnsTrue()
    {
        Assert.True(Ed25519Verifier.ConstantTimeEquals(null, null));
    }

    [Fact]
    public void ConstantTimeEquals_WithFirstNull_ReturnsFalse()
    {
        Assert.False(Ed25519Verifier.ConstantTimeEquals(null, "test"));
    }

    [Fact]
    public void ConstantTimeEquals_WithSecondNull_ReturnsFalse()
    {
        Assert.False(Ed25519Verifier.ConstantTimeEquals("test", null));
    }

    [Fact]
    public void ConstantTimeEquals_WithEmptyStrings_ReturnsTrue()
    {
        Assert.True(Ed25519Verifier.ConstantTimeEquals("", ""));
    }

    [Fact]
    public void ConstantTimeEquals_WithLicenseKeys_ComparesCorrectly()
    {
        // Realistic license key comparison
        Assert.True(Ed25519Verifier.ConstantTimeEquals("ABC-123-DEF-456", "ABC-123-DEF-456"));
        Assert.False(Ed25519Verifier.ConstantTimeEquals("ABC-123-DEF-456", "ABC-123-DEF-457"));
    }
}
