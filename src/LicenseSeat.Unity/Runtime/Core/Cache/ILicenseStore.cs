#nullable enable
namespace LicenseSeat
{

    /// <summary>
    /// Durable storage for the SDK's license state, so an activation and its
    /// signed offline token survive application and operating-system restarts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The store is a plain, untrusted key/value container. The SDK owns the
    /// format of every entry and re-validates everything it reads: offline
    /// tokens are re-verified (Ed25519 signature, canonical payload, license,
    /// product, device, time window and local grace policy) every time they
    /// are consulted, cached validation results and entitlements are never
    /// restored from storage, and signing keys are never persisted. Editing
    /// or replacing stored entries therefore cannot manufacture authority.
    /// </para>
    /// <para>
    /// Entry names are safe identifiers (<c>[A-Za-z0-9._-]</c>, starting with
    /// an alphanumeric character) derived from
    /// <see cref="LicenseSeatClientOptions.StoragePrefix"/>.
    /// </para>
    /// <para>
    /// Implementations may throw from any member. A failed read is treated as
    /// "no stored value" and a failed write or delete is logged and ignored;
    /// a storage fault never fails a licensing operation.
    /// <c>FileLicenseStore</c> (core package only) is the built-in implementation for
    /// desktop and server applications.
    /// </para>
    /// </remarks>
    public interface ILicenseStore
    {
        /// <summary>Returns the stored text for an entry, or <c>null</c> when it does not exist.</summary>
        /// <param name="entryName">The entry name.</param>
        /// <returns>The stored text, or <c>null</c>.</returns>
        string? Read(string entryName);

        /// <summary>Stores text for an entry, replacing any previous value atomically.</summary>
        /// <param name="entryName">The entry name.</param>
        /// <param name="contents">The text to store.</param>
        void Write(string entryName, string contents);

        /// <summary>Deletes an entry. Deleting an entry that does not exist is not an error.</summary>
        /// <param name="entryName">The entry name.</param>
        void Delete(string entryName);
    }
}
