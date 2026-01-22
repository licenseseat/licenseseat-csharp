using System;
using System.Collections.Generic;

namespace LicenseSeat;

/// <summary>
/// Represents the current status of a license.
/// </summary>
public sealed class LicenseStatus
{
    /// <summary>
    /// Gets the status type.
    /// </summary>
    public LicenseStatusType StatusType { get; }

    /// <summary>
    /// Gets the status message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the license details if the license is active.
    /// </summary>
    public LicenseStatusDetails? Details { get; }

    private LicenseStatus(LicenseStatusType statusType, string message, LicenseStatusDetails? details = null)
    {
        StatusType = statusType;
        Message = message;
        Details = details;
    }

    /// <summary>
    /// Gets a value indicating whether the license is considered valid (active or offline-valid).
    /// </summary>
    public bool IsValid => StatusType == LicenseStatusType.Active || StatusType == LicenseStatusType.OfflineValid;

    /// <summary>
    /// Gets a value indicating whether validation is pending.
    /// </summary>
    public bool IsPending => StatusType == LicenseStatusType.Pending;

    /// <summary>
    /// Creates an inactive status (no license activated).
    /// </summary>
    /// <param name="message">The status message.</param>
    /// <returns>A new <see cref="LicenseStatus"/>.</returns>
    public static LicenseStatus Inactive(string message = "No license activated")
        => new(LicenseStatusType.Inactive, message);

    /// <summary>
    /// Creates a pending status (validation in progress).
    /// </summary>
    /// <param name="message">The status message.</param>
    /// <returns>A new <see cref="LicenseStatus"/>.</returns>
    public static LicenseStatus Pending(string message = "License pending validation")
        => new(LicenseStatusType.Pending, message);

    /// <summary>
    /// Creates an active status (license is valid).
    /// </summary>
    /// <param name="details">The license details.</param>
    /// <returns>A new <see cref="LicenseStatus"/>.</returns>
    public static LicenseStatus Active(LicenseStatusDetails details)
        => new(LicenseStatusType.Active, "License is active", details);

    /// <summary>
    /// Creates an invalid status (validation failed).
    /// </summary>
    /// <param name="message">The status message.</param>
    /// <returns>A new <see cref="LicenseStatus"/>.</returns>
    public static LicenseStatus Invalid(string message = "License is invalid")
        => new(LicenseStatusType.Invalid, message);

    /// <summary>
    /// Creates an offline-valid status (license validated offline).
    /// </summary>
    /// <param name="details">The license details.</param>
    /// <returns>A new <see cref="LicenseStatus"/>.</returns>
    public static LicenseStatus OfflineValid(LicenseStatusDetails details)
        => new(LicenseStatusType.OfflineValid, "License is valid (offline)", details);

    /// <summary>
    /// Creates an offline-invalid status (offline validation failed).
    /// </summary>
    /// <param name="message">The status message.</param>
    /// <returns>A new <see cref="LicenseStatus"/>.</returns>
    public static LicenseStatus OfflineInvalid(string message = "License invalid (offline)")
        => new(LicenseStatusType.OfflineInvalid, message);
}

/// <summary>
/// The type of license status.
/// </summary>
public enum LicenseStatusType
{
    /// <summary>
    /// No license is activated.
    /// </summary>
    Inactive,

    /// <summary>
    /// License validation is pending.
    /// </summary>
    Pending,

    /// <summary>
    /// License is active and validated online.
    /// </summary>
    Active,

    /// <summary>
    /// License validation failed.
    /// </summary>
    Invalid,

    /// <summary>
    /// License is valid (validated offline).
    /// </summary>
    OfflineValid,

    /// <summary>
    /// License is invalid (offline validation failed).
    /// </summary>
    OfflineInvalid
}

/// <summary>
/// Details about an active license.
/// </summary>
public sealed class LicenseStatusDetails
{
    /// <summary>
    /// Gets or sets the license key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device ID.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the license was activated.
    /// </summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the license was last validated.
    /// </summary>
    public DateTimeOffset? LastValidated { get; set; }

    /// <summary>
    /// Gets or sets the active entitlements.
    /// </summary>
    public IReadOnlyList<Entitlement> Entitlements { get; set; } = Array.Empty<Entitlement>();
}
