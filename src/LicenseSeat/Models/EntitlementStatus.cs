using System;

namespace LicenseSeat;

/// <summary>
/// Represents the status of an entitlement check.
/// </summary>
public sealed class EntitlementStatus
{
    /// <summary>
    /// Gets a value indicating whether the entitlement is active.
    /// </summary>
    public bool Active { get; }

    /// <summary>
    /// Gets the reason why the entitlement is not active, if applicable.
    /// </summary>
    public EntitlementInactiveReason? Reason { get; }

    /// <summary>
    /// Gets when the entitlement expires, if it has an expiration date.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Gets the entitlement data if it was found.
    /// </summary>
    public Entitlement? Entitlement { get; }

    /// <summary>
    /// Creates a new instance of <see cref="EntitlementStatus"/>.
    /// </summary>
    /// <param name="active">Whether the entitlement is active.</param>
    /// <param name="reason">The reason for inactivity.</param>
    /// <param name="expiresAt">When the entitlement expires.</param>
    /// <param name="entitlement">The entitlement data.</param>
    public EntitlementStatus(
        bool active,
        EntitlementInactiveReason? reason = null,
        DateTimeOffset? expiresAt = null,
        Entitlement? entitlement = null)
    {
        Active = active;
        Reason = reason;
        ExpiresAt = expiresAt;
        Entitlement = entitlement;
    }

    /// <summary>
    /// Creates an active entitlement status.
    /// </summary>
    /// <param name="entitlement">The entitlement.</param>
    /// <returns>An active <see cref="EntitlementStatus"/>.</returns>
    public static EntitlementStatus ActiveStatus(Entitlement entitlement)
        => new(true, null, entitlement.ExpiresAt, entitlement);

    /// <summary>
    /// Creates an inactive entitlement status because no license is active.
    /// </summary>
    /// <returns>An inactive <see cref="EntitlementStatus"/>.</returns>
    public static EntitlementStatus NoLicense()
        => new(false, EntitlementInactiveReason.NoLicense);

    /// <summary>
    /// Creates an inactive entitlement status because the entitlement was not found.
    /// </summary>
    /// <returns>An inactive <see cref="EntitlementStatus"/>.</returns>
    public static EntitlementStatus NotFound()
        => new(false, EntitlementInactiveReason.NotFound);

    /// <summary>
    /// Creates an inactive entitlement status because the entitlement has expired.
    /// </summary>
    /// <param name="entitlement">The expired entitlement.</param>
    /// <returns>An inactive <see cref="EntitlementStatus"/>.</returns>
    public static EntitlementStatus Expired(Entitlement entitlement)
        => new(false, EntitlementInactiveReason.Expired, entitlement.ExpiresAt, entitlement);
}

/// <summary>
/// The reason why an entitlement is not active.
/// </summary>
public enum EntitlementInactiveReason
{
    /// <summary>
    /// No license is currently active.
    /// </summary>
    NoLicense,

    /// <summary>
    /// The entitlement was not found in the license.
    /// </summary>
    NotFound,

    /// <summary>
    /// The entitlement has expired.
    /// </summary>
    Expired
}
