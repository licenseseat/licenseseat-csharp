using System;
#if !NETSTANDARD2_0
using System.Runtime.CompilerServices;
#endif

namespace LicenseSeat;

/// <summary>
/// Base exception class for all LicenseSeat SDK errors.
/// </summary>
public class LicenseSeatException : Exception
{
    /// <summary>
    /// Gets the error code associated with this exception.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseSeatException"/> class.
    /// </summary>
    public LicenseSeatException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseSeatException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public LicenseSeatException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseSeatException"/> class
    /// with a specified error message and error code.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="errorCode">The error code.</param>
    public LicenseSeatException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseSeatException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LicenseSeatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseSeatException"/> class
    /// with a specified error message, error code, and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LicenseSeatException(string message, string errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when an API request fails.
/// </summary>
public class ApiException : LicenseSeatException
{
    /// <summary>
    /// Gets the HTTP status code returned by the API.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the error code from the API response.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Gets the raw response body from the API, if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiException"/> class.
    /// </summary>
    /// <param name="message">The error message from the API.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    public ApiException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiException"/> class.
    /// </summary>
    /// <param name="message">The error message from the API.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The error code from the API.</param>
    public ApiException(string message, int statusCode, string? code)
        : base(message, code ?? string.Empty)
    {
        StatusCode = statusCode;
        Code = code;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiException"/> class.
    /// </summary>
    /// <param name="message">The error message from the API.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The error code from the API.</param>
    /// <param name="responseBody">The raw response body.</param>
    public ApiException(string message, int statusCode, string? code, string? responseBody)
        : base(message, code ?? string.Empty)
    {
        StatusCode = statusCode;
        Code = code;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="innerException">The inner exception.</param>
    public ApiException(string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets a value indicating whether this error is a network-related failure.
    /// </summary>
    public bool IsNetworkError => StatusCode == 0 || StatusCode == 408;

    /// <summary>
    /// Gets a value indicating whether this error is a server-side error (5xx).
    /// </summary>
    public bool IsServerError => StatusCode >= 500 && StatusCode < 600;

    /// <summary>
    /// Gets a value indicating whether this error is a client-side error (4xx).
    /// </summary>
    public bool IsClientError => StatusCode >= 400 && StatusCode < 500;

    /// <summary>
    /// Gets a value indicating whether this error should trigger a retry.
    /// </summary>
    public bool IsRetryable =>
        IsNetworkError ||
        StatusCode == 429 ||
        (StatusCode >= 502 && StatusCode < 600);
}

/// <summary>
/// Exception thrown for license-related errors.
/// </summary>
public class LicenseException : LicenseSeatException
{
    /// <summary>
    /// Error code for when no active license is found.
    /// </summary>
    public const string NoLicenseCode = "no_license";

    /// <summary>
    /// Error code for when the license is invalid.
    /// </summary>
    public const string InvalidLicenseCode = "invalid_license";

    /// <summary>
    /// Error code for when the license has expired.
    /// </summary>
    public const string ExpiredCode = "expired";

    /// <summary>
    /// Error code for when the license has been revoked.
    /// </summary>
    public const string RevokedCode = "revoked";

    /// <summary>
    /// Error code for when the license key doesn't match.
    /// </summary>
    public const string LicenseMismatchCode = "license_mismatch";

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    public LicenseException(string message, string errorCode)
        : base(message, errorCode)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public LicenseException(string message, string errorCode, Exception innerException)
        : base(message, errorCode, innerException)
    {
    }

    /// <summary>
    /// Creates a new exception indicating no active license was found.
    /// </summary>
    /// <returns>A new <see cref="LicenseException"/>.</returns>
    public static LicenseException NoActiveLicense()
        => new("No active license found", NoLicenseCode);

    /// <summary>
    /// Creates a new exception indicating the license is invalid.
    /// </summary>
    /// <param name="reason">The reason why the license is invalid.</param>
    /// <returns>A new <see cref="LicenseException"/>.</returns>
    public static LicenseException InvalidLicense(string? reason = null)
        => new(reason ?? "License is invalid", InvalidLicenseCode);

    /// <summary>
    /// Creates a new exception indicating the license has expired.
    /// </summary>
    /// <returns>A new <see cref="LicenseException"/>.</returns>
    public static LicenseException Expired()
        => new("License has expired", ExpiredCode);

    /// <summary>
    /// Creates a new exception indicating the license has been revoked.
    /// </summary>
    /// <returns>A new <see cref="LicenseException"/>.</returns>
    public static LicenseException Revoked()
        => new("License has been revoked", RevokedCode);
}

/// <summary>
/// Exception thrown for configuration errors.
/// </summary>
public class ConfigurationException : LicenseSeatException
{
    /// <summary>
    /// Error code for missing API key.
    /// </summary>
    public const string MissingApiKeyCode = "missing_api_key";

    /// <summary>
    /// Error code for invalid configuration.
    /// </summary>
    public const string InvalidConfigurationCode = "invalid_configuration";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ConfigurationException(string message)
        : base(message, InvalidConfigurationCode)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    public ConfigurationException(string message, string errorCode)
        : base(message, errorCode)
    {
    }

    /// <summary>
    /// Creates a new exception indicating the API key is required but not configured.
    /// </summary>
    /// <returns>A new <see cref="ConfigurationException"/>.</returns>
    public static ConfigurationException ApiKeyRequired()
        => new("API key is required for this operation", MissingApiKeyCode);
}

/// <summary>
/// Exception thrown for cryptographic operation errors.
/// </summary>
public class CryptoException : LicenseSeatException
{
    /// <summary>
    /// Error code for signature verification failure.
    /// </summary>
    public const string SignatureInvalidCode = "signature_invalid";

    /// <summary>
    /// Error code for missing public key.
    /// </summary>
    public const string NoPublicKeyCode = "no_public_key";

    /// <summary>
    /// Error code for clock tampering detection.
    /// </summary>
    public const string ClockTamperCode = "clock_tamper";

    /// <summary>
    /// Error code for general verification errors.
    /// </summary>
    public const string VerificationErrorCode = "verification_error";

    /// <summary>
    /// Error code for invalid key format or length.
    /// </summary>
    public const string InvalidKeyCode = "invalid_key";

    /// <summary>
    /// Error code for invalid signature format or length.
    /// </summary>
    public const string InvalidSignatureCode = "invalid_signature";

    /// <summary>
    /// Error code when signature verification fails.
    /// </summary>
    public const string VerificationFailedCode = "verification_failed";

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CryptoException(string message)
        : base(message, VerificationErrorCode)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    public CryptoException(string message, string errorCode)
        : base(message, errorCode)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CryptoException(string message, Exception innerException)
        : base(message, VerificationErrorCode, innerException)
    {
    }

    /// <summary>
    /// Creates a new exception indicating signature verification failed.
    /// </summary>
    /// <returns>A new <see cref="CryptoException"/>.</returns>
    public static CryptoException SignatureInvalid()
        => new("Offline license signature verification failed", SignatureInvalidCode);

    /// <summary>
    /// Creates a new exception indicating the public key is missing.
    /// </summary>
    /// <returns>A new <see cref="CryptoException"/>.</returns>
    public static CryptoException NoPublicKey()
        => new("Public key not found for offline verification", NoPublicKeyCode);

    /// <summary>
    /// Creates a new exception indicating clock tampering was detected.
    /// </summary>
    /// <returns>A new <see cref="CryptoException"/>.</returns>
    public static CryptoException ClockTamper()
        => new("System clock appears to have been tampered with", ClockTamperCode);
}
