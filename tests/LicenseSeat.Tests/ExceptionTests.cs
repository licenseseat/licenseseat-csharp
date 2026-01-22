using System;

namespace LicenseSeat.Tests;

public class ExceptionTests
{
    public class LicenseSeatExceptionTests
    {
        [Fact]
        public void Constructor_Default_CreatesException()
        {
            var exception = new LicenseSeatException();

            Assert.NotNull(exception);
            Assert.Null(exception.ErrorCode);
        }

        [Fact]
        public void Constructor_WithMessage_SetsMessage()
        {
            var exception = new LicenseSeatException("Test message");

            Assert.Equal("Test message", exception.Message);
            Assert.Null(exception.ErrorCode);
        }

        [Fact]
        public void Constructor_WithMessageAndErrorCode_SetsBoth()
        {
            var exception = new LicenseSeatException("Test message", "test_code");

            Assert.Equal("Test message", exception.Message);
            Assert.Equal("test_code", exception.ErrorCode);
        }

        [Fact]
        public void Constructor_WithMessageAndInnerException_SetsBoth()
        {
            var inner = new InvalidOperationException("Inner");
            var exception = new LicenseSeatException("Outer", inner);

            Assert.Equal("Outer", exception.Message);
            Assert.Same(inner, exception.InnerException);
        }

        [Fact]
        public void Constructor_WithAllParameters_SetsAll()
        {
            var inner = new InvalidOperationException("Inner");
            var exception = new LicenseSeatException("Outer", "test_code", inner);

            Assert.Equal("Outer", exception.Message);
            Assert.Equal("test_code", exception.ErrorCode);
            Assert.Same(inner, exception.InnerException);
        }
    }

    public class ApiExceptionTests
    {
        [Fact]
        public void Constructor_WithMessageAndStatusCode_SetsBoth()
        {
            var exception = new ApiException("Not found", 404);

            Assert.Equal("Not found", exception.Message);
            Assert.Equal(404, exception.StatusCode);
            Assert.Null(exception.Code);
            Assert.Null(exception.ResponseBody);
        }

        [Fact]
        public void Constructor_WithCode_SetsCode()
        {
            var exception = new ApiException("Not found", 404, "license_not_found");

            Assert.Equal("license_not_found", exception.Code);
            Assert.Equal("license_not_found", exception.ErrorCode);
        }

        [Fact]
        public void Constructor_WithResponseBody_SetsResponseBody()
        {
            var exception = new ApiException("Error", 500, "server_error", "{\"error\":\"internal\"}");

            Assert.Equal("{\"error\":\"internal\"}", exception.ResponseBody);
        }

        [Fact]
        public void Constructor_WithInnerException_SetsInnerException()
        {
            var inner = new InvalidOperationException("Inner");
            var exception = new ApiException("Error", 500, inner);

            Assert.Same(inner, exception.InnerException);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(408, true)]
        [InlineData(404, false)]
        [InlineData(500, false)]
        public void IsNetworkError_ReturnsCorrectValue(int statusCode, bool expected)
        {
            var exception = new ApiException("Error", statusCode);

            Assert.Equal(expected, exception.IsNetworkError);
        }

        [Theory]
        [InlineData(500, true)]
        [InlineData(503, true)]
        [InlineData(599, true)]
        [InlineData(400, false)]
        [InlineData(499, false)]
        [InlineData(600, false)]
        public void IsServerError_ReturnsCorrectValue(int statusCode, bool expected)
        {
            var exception = new ApiException("Error", statusCode);

            Assert.Equal(expected, exception.IsServerError);
        }

        [Theory]
        [InlineData(400, true)]
        [InlineData(404, true)]
        [InlineData(499, true)]
        [InlineData(399, false)]
        [InlineData(500, false)]
        public void IsClientError_ReturnsCorrectValue(int statusCode, bool expected)
        {
            var exception = new ApiException("Error", statusCode);

            Assert.Equal(expected, exception.IsClientError);
        }

        [Theory]
        [InlineData(0, true)]    // Network error
        [InlineData(408, true)]  // Request timeout
        [InlineData(429, true)]  // Rate limited
        [InlineData(502, true)]  // Bad gateway
        [InlineData(503, true)]  // Service unavailable
        [InlineData(504, true)]  // Gateway timeout
        [InlineData(400, false)] // Bad request
        [InlineData(401, false)] // Unauthorized
        [InlineData(404, false)] // Not found
        [InlineData(500, false)] // Internal server error (not retryable)
        [InlineData(501, false)] // Not implemented (not retryable)
        public void IsRetryable_ReturnsCorrectValue(int statusCode, bool expected)
        {
            var exception = new ApiException("Error", statusCode);

            Assert.Equal(expected, exception.IsRetryable);
        }
    }

    public class LicenseExceptionTests
    {
        [Fact]
        public void Constructor_SetsMessageAndErrorCode()
        {
            var exception = new LicenseException("Test", "test_code");

            Assert.Equal("Test", exception.Message);
            Assert.Equal("test_code", exception.ErrorCode);
        }

        [Fact]
        public void Constructor_WithInnerException_SetsInnerException()
        {
            var inner = new InvalidOperationException("Inner");
            var exception = new LicenseException("Outer", "test_code", inner);

            Assert.Same(inner, exception.InnerException);
        }

        [Fact]
        public void NoActiveLicense_ReturnsCorrectException()
        {
            var exception = LicenseException.NoActiveLicense();

            Assert.Equal("No active license found", exception.Message);
            Assert.Equal(LicenseException.NoLicenseCode, exception.ErrorCode);
        }

        [Fact]
        public void InvalidLicense_WithoutReason_ReturnsDefaultMessage()
        {
            var exception = LicenseException.InvalidLicense();

            Assert.Equal("License is invalid", exception.Message);
            Assert.Equal(LicenseException.InvalidLicenseCode, exception.ErrorCode);
        }

        [Fact]
        public void InvalidLicense_WithReason_ReturnsCustomMessage()
        {
            var exception = LicenseException.InvalidLicense("License has been suspended");

            Assert.Equal("License has been suspended", exception.Message);
            Assert.Equal(LicenseException.InvalidLicenseCode, exception.ErrorCode);
        }

        [Fact]
        public void Expired_ReturnsCorrectException()
        {
            var exception = LicenseException.Expired();

            Assert.Equal("License has expired", exception.Message);
            Assert.Equal(LicenseException.ExpiredCode, exception.ErrorCode);
        }

        [Fact]
        public void Revoked_ReturnsCorrectException()
        {
            var exception = LicenseException.Revoked();

            Assert.Equal("License has been revoked", exception.Message);
            Assert.Equal(LicenseException.RevokedCode, exception.ErrorCode);
        }
    }

    public class ConfigurationExceptionTests
    {
        [Fact]
        public void Constructor_WithMessage_SetsMessageAndDefaultCode()
        {
            var exception = new ConfigurationException("Config error");

            Assert.Equal("Config error", exception.Message);
            Assert.Equal(ConfigurationException.InvalidConfigurationCode, exception.ErrorCode);
        }

        [Fact]
        public void Constructor_WithMessageAndCode_SetsBoth()
        {
            var exception = new ConfigurationException("Custom error", "custom_code");

            Assert.Equal("Custom error", exception.Message);
            Assert.Equal("custom_code", exception.ErrorCode);
        }

        [Fact]
        public void ApiKeyRequired_ReturnsCorrectException()
        {
            var exception = ConfigurationException.ApiKeyRequired();

            Assert.Contains("API key", exception.Message);
            Assert.Equal(ConfigurationException.MissingApiKeyCode, exception.ErrorCode);
        }
    }

    public class CryptoExceptionTests
    {
        [Fact]
        public void Constructor_WithMessage_SetsMessageAndDefaultCode()
        {
            var exception = new CryptoException("Crypto error");

            Assert.Equal("Crypto error", exception.Message);
            Assert.Equal(CryptoException.VerificationErrorCode, exception.ErrorCode);
        }

        [Fact]
        public void Constructor_WithMessageAndCode_SetsBoth()
        {
            var exception = new CryptoException("Custom error", "custom_code");

            Assert.Equal("Custom error", exception.Message);
            Assert.Equal("custom_code", exception.ErrorCode);
        }

        [Fact]
        public void Constructor_WithInnerException_SetsInnerException()
        {
            var inner = new InvalidOperationException("Inner");
            var exception = new CryptoException("Outer", inner);

            Assert.Same(inner, exception.InnerException);
            Assert.Equal(CryptoException.VerificationErrorCode, exception.ErrorCode);
        }

        [Fact]
        public void SignatureInvalid_ReturnsCorrectException()
        {
            var exception = CryptoException.SignatureInvalid();

            Assert.Contains("signature", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(CryptoException.SignatureInvalidCode, exception.ErrorCode);
        }

        [Fact]
        public void NoPublicKey_ReturnsCorrectException()
        {
            var exception = CryptoException.NoPublicKey();

            Assert.Contains("public key", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(CryptoException.NoPublicKeyCode, exception.ErrorCode);
        }

        [Fact]
        public void ClockTamper_ReturnsCorrectException()
        {
            var exception = CryptoException.ClockTamper();

            Assert.Contains("clock", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(CryptoException.ClockTamperCode, exception.ErrorCode);
        }
    }
}
