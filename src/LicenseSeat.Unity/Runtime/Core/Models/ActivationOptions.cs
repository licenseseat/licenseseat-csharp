using System.Collections.Generic;

namespace LicenseSeat;

/// <summary>
/// Options for license activation.
/// </summary>
public sealed class ActivationOptions
{
    /// <summary>
    /// Gets or sets a custom device identifier.
    /// If not set, a device identifier will be automatically generated.
    /// </summary>
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the software release date for version-locked licenses.
    /// Format: ISO 8601 date string (e.g., "2024-01-15").
    /// </summary>
    public string? SoftwareReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets additional metadata to store with the activation.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creates a new instance of <see cref="ActivationOptions"/> with default values.
    /// </summary>
    public ActivationOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ActivationOptions"/> with the specified device identifier.
    /// </summary>
    /// <param name="deviceIdentifier">The device identifier.</param>
    public ActivationOptions(string deviceIdentifier)
    {
        DeviceIdentifier = deviceIdentifier;
    }
}

/// <summary>
/// Options for license validation.
/// </summary>
public sealed class ValidationOptions
{
    /// <summary>
    /// Gets or sets a custom device identifier to use for validation.
    /// If not set, the cached device identifier will be used.
    /// </summary>
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets a specific product slug to validate against.
    /// </summary>
    public string? ProductSlug { get; set; }

    /// <summary>
    /// Creates a new instance of <see cref="ValidationOptions"/> with default values.
    /// </summary>
    public ValidationOptions()
    {
    }
}
