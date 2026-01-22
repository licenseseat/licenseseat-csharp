using System.Collections.Generic;

namespace LicenseSeat;

/// <summary>
/// Options for license activation.
/// </summary>
public sealed class ActivationOptions
{
    /// <summary>
    /// Gets or sets a custom device ID.
    /// If not set, a device ID will be automatically generated.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the device name (human-readable).
    /// </summary>
    public string? DeviceName { get; set; }

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
    /// Creates a new instance of <see cref="ActivationOptions"/> with the specified device ID.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    public ActivationOptions(string deviceId)
    {
        DeviceId = deviceId;
    }
}

/// <summary>
/// Options for license validation.
/// </summary>
public sealed class ValidationOptions
{
    /// <summary>
    /// Gets or sets a custom device ID to use for validation.
    /// If not set, the cached device ID will be used.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Creates a new instance of <see cref="ValidationOptions"/> with default values.
    /// </summary>
    public ValidationOptions()
    {
    }
}
