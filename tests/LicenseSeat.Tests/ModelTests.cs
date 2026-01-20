using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LicenseSeat.Tests;

public class ModelTests
{
    public class EntitlementTests
    {
        [Fact]
        public void Constructor_Default_CreatesEmptyEntitlement()
        {
            var entitlement = new Entitlement();

            Assert.Equal(string.Empty, entitlement.Key);
            Assert.Null(entitlement.Name);
            Assert.Null(entitlement.ExpiresAt);
        }

        [Fact]
        public void Constructor_WithKey_SetsKey()
        {
            var entitlement = new Entitlement("pro-features");

            Assert.Equal("pro-features", entitlement.Key);
        }

        [Fact]
        public void Constructor_WithNullKey_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new Entitlement(null!));
        }

        [Fact]
        public void Constructor_WithKeyAndExpiration_SetsBoth()
        {
            var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
            var entitlement = new Entitlement("pro-features", expiresAt);

            Assert.Equal("pro-features", entitlement.Key);
            Assert.Equal(expiresAt, entitlement.ExpiresAt);
        }

        [Fact]
        public void IsExpired_WhenNoExpiration_ReturnsFalse()
        {
            var entitlement = new Entitlement("test");

            Assert.False(entitlement.IsExpired);
        }

        [Fact]
        public void IsExpired_WhenNotExpired_ReturnsFalse()
        {
            var entitlement = new Entitlement("test", DateTimeOffset.UtcNow.AddDays(30));

            Assert.False(entitlement.IsExpired);
        }

        [Fact]
        public void IsExpired_WhenExpired_ReturnsTrue()
        {
            var entitlement = new Entitlement("test", DateTimeOffset.UtcNow.AddDays(-1));

            Assert.True(entitlement.IsExpired);
        }

        [Fact]
        public void IsActive_WhenNotExpired_ReturnsTrue()
        {
            var entitlement = new Entitlement("test", DateTimeOffset.UtcNow.AddDays(30));

            Assert.True(entitlement.IsActive);
        }

        [Fact]
        public void IsActive_WhenExpired_ReturnsFalse()
        {
            var entitlement = new Entitlement("test", DateTimeOffset.UtcNow.AddDays(-1));

            Assert.False(entitlement.IsActive);
        }

        [Fact]
        public void JsonSerialization_RoundTrips()
        {
            var entitlement = new Entitlement
            {
                Key = "updates",
                Name = "Software Updates",
                ExpiresAt = DateTimeOffset.Parse("2025-12-31T23:59:59Z", System.Globalization.CultureInfo.InvariantCulture)
            };

            var json = JsonSerializer.Serialize(entitlement);
            var deserialized = JsonSerializer.Deserialize<Entitlement>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(entitlement.Key, deserialized.Key);
            Assert.Equal(entitlement.Name, deserialized.Name);
            Assert.Equal(entitlement.ExpiresAt, deserialized.ExpiresAt);
        }
    }

    public class LicenseTests
    {
        [Fact]
        public void Constructor_Default_CreatesEmptyLicense()
        {
            var license = new License();

            Assert.Equal(string.Empty, license.LicenseKey);
            Assert.Equal(string.Empty, license.DeviceIdentifier);
            Assert.Null(license.Status);
        }

        [Fact]
        public void IsExpired_WhenNoEndDate_ReturnsFalse()
        {
            var license = new License { EndsAt = null };

            Assert.False(license.IsExpired);
        }

        [Fact]
        public void IsExpired_WhenNotExpired_ReturnsFalse()
        {
            var license = new License { EndsAt = DateTimeOffset.UtcNow.AddDays(30) };

            Assert.False(license.IsExpired);
        }

        [Fact]
        public void IsExpired_WhenExpired_ReturnsTrue()
        {
            var license = new License { EndsAt = DateTimeOffset.UtcNow.AddDays(-1) };

            Assert.True(license.IsExpired);
        }

        [Fact]
        public void IsActive_WhenActiveAndNotExpired_ReturnsTrue()
        {
            var license = new License
            {
                Status = "active",
                EndsAt = DateTimeOffset.UtcNow.AddDays(30)
            };

            Assert.True(license.IsActive);
        }

        [Fact]
        public void IsActive_WhenActiveButExpired_ReturnsFalse()
        {
            var license = new License
            {
                Status = "active",
                EndsAt = DateTimeOffset.UtcNow.AddDays(-1)
            };

            Assert.False(license.IsActive);
        }

        [Fact]
        public void IsActive_WhenNotActive_ReturnsFalse()
        {
            var license = new License { Status = "suspended" };

            Assert.False(license.IsActive);
        }

        [Fact]
        public void JsonSerialization_RoundTrips()
        {
            var license = new License
            {
                LicenseKey = "XXXX-XXXX-XXXX-XXXX",
                DeviceIdentifier = "device-123",
                Status = "active",
                PlanKey = "pro",
                SeatLimit = 5,
                ActiveActivationsCount = 2
            };

            var json = JsonSerializer.Serialize(license);
            var deserialized = JsonSerializer.Deserialize<License>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(license.LicenseKey, deserialized.LicenseKey);
            Assert.Equal(license.Status, deserialized.Status);
            Assert.Equal(license.PlanKey, deserialized.PlanKey);
        }
    }

    public class ValidationResultTests
    {
        [Fact]
        public void Success_CreatesValidResult()
        {
            var result = ValidationResult.Success();

            Assert.True(result.Valid);
            Assert.Null(result.Reason);
            Assert.Null(result.ReasonCode);
            Assert.False(result.Offline);
        }

        [Fact]
        public void Success_WithLicense_IncludesLicense()
        {
            var license = new License { LicenseKey = "test-key" };
            var result = ValidationResult.Success(license);

            Assert.True(result.Valid);
            Assert.NotNull(result.License);
            Assert.Equal("test-key", result.License.LicenseKey);
        }

        [Fact]
        public void Failed_CreatesInvalidResult()
        {
            var result = ValidationResult.Failed("License expired", "expired");

            Assert.False(result.Valid);
            Assert.Equal("License expired", result.Reason);
            Assert.Equal("expired", result.ReasonCode);
        }

        [Fact]
        public void OfflineResult_Valid_CreatesValidOfflineResult()
        {
            var entitlements = new List<Entitlement> { new("pro") };
            var result = ValidationResult.OfflineResult(true, entitlements: entitlements);

            Assert.True(result.Valid);
            Assert.True(result.Offline);
            Assert.NotNull(result.ActiveEntitlements);
            Assert.Single(result.ActiveEntitlements);
        }

        [Fact]
        public void OfflineResult_Invalid_CreatesInvalidOfflineResult()
        {
            var result = ValidationResult.OfflineResult(false, "signature_invalid");

            Assert.False(result.Valid);
            Assert.True(result.Offline);
            Assert.Equal("signature_invalid", result.ReasonCode);
        }
    }

    public class LicenseStatusTests
    {
        [Fact]
        public void Inactive_CreatesInactiveStatus()
        {
            var status = LicenseStatus.Inactive();

            Assert.Equal(LicenseStatusType.Inactive, status.StatusType);
            Assert.Equal("No license activated", status.Message);
            Assert.Null(status.Details);
            Assert.False(status.IsValid);
        }

        [Fact]
        public void Pending_CreatesPendingStatus()
        {
            var status = LicenseStatus.Pending();

            Assert.Equal(LicenseStatusType.Pending, status.StatusType);
            Assert.True(status.IsPending);
            Assert.False(status.IsValid);
        }

        [Fact]
        public void Active_CreatesActiveStatus()
        {
            var details = new LicenseStatusDetails
            {
                LicenseKey = "test-key",
                DeviceIdentifier = "device-123"
            };

            var status = LicenseStatus.Active(details);

            Assert.Equal(LicenseStatusType.Active, status.StatusType);
            Assert.True(status.IsValid);
            Assert.NotNull(status.Details);
            Assert.Equal("test-key", status.Details.LicenseKey);
        }

        [Fact]
        public void Invalid_CreatesInvalidStatus()
        {
            var status = LicenseStatus.Invalid("License revoked");

            Assert.Equal(LicenseStatusType.Invalid, status.StatusType);
            Assert.Equal("License revoked", status.Message);
            Assert.False(status.IsValid);
        }

        [Fact]
        public void OfflineValid_CreatesOfflineValidStatus()
        {
            var details = new LicenseStatusDetails { LicenseKey = "test" };
            var status = LicenseStatus.OfflineValid(details);

            Assert.Equal(LicenseStatusType.OfflineValid, status.StatusType);
            Assert.True(status.IsValid);
        }

        [Fact]
        public void OfflineInvalid_CreatesOfflineInvalidStatus()
        {
            var status = LicenseStatus.OfflineInvalid("Signature invalid");

            Assert.Equal(LicenseStatusType.OfflineInvalid, status.StatusType);
            Assert.False(status.IsValid);
        }
    }

    public class EntitlementStatusTests
    {
        [Fact]
        public void ActiveStatus_CreatesActiveEntitlementStatus()
        {
            var entitlement = new Entitlement("pro", DateTimeOffset.UtcNow.AddDays(30));
            var status = EntitlementStatus.ActiveStatus(entitlement);

            Assert.True(status.Active);
            Assert.Null(status.Reason);
            Assert.NotNull(status.Entitlement);
            Assert.Equal(entitlement.ExpiresAt, status.ExpiresAt);
        }

        [Fact]
        public void NoLicense_CreatesNoLicenseStatus()
        {
            var status = EntitlementStatus.NoLicense();

            Assert.False(status.Active);
            Assert.Equal(EntitlementInactiveReason.NoLicense, status.Reason);
        }

        [Fact]
        public void NotFound_CreatesNotFoundStatus()
        {
            var status = EntitlementStatus.NotFound();

            Assert.False(status.Active);
            Assert.Equal(EntitlementInactiveReason.NotFound, status.Reason);
        }

        [Fact]
        public void Expired_CreatesExpiredStatus()
        {
            var entitlement = new Entitlement("pro", DateTimeOffset.UtcNow.AddDays(-1));
            var status = EntitlementStatus.Expired(entitlement);

            Assert.False(status.Active);
            Assert.Equal(EntitlementInactiveReason.Expired, status.Reason);
            Assert.NotNull(status.Entitlement);
        }
    }

    public class ActivationOptionsTests
    {
        [Fact]
        public void Constructor_Default_CreatesEmptyOptions()
        {
            var options = new ActivationOptions();

            Assert.Null(options.DeviceIdentifier);
            Assert.Null(options.SoftwareReleaseDate);
            Assert.Null(options.Metadata);
        }

        [Fact]
        public void Constructor_WithDeviceIdentifier_SetsDeviceIdentifier()
        {
            var options = new ActivationOptions("device-123");

            Assert.Equal("device-123", options.DeviceIdentifier);
        }

        [Fact]
        public void Metadata_CanBeSet()
        {
            var options = new ActivationOptions
            {
                Metadata = new Dictionary<string, object>
                {
                    ["version"] = "1.0.0",
                    ["platform"] = "Windows"
                }
            };

            Assert.NotNull(options.Metadata);
            Assert.Equal("1.0.0", options.Metadata["version"]);
        }
    }

    public class ValidationOptionsTests
    {
        [Fact]
        public void Constructor_Default_CreatesEmptyOptions()
        {
            var options = new ValidationOptions();

            Assert.Null(options.DeviceIdentifier);
            Assert.Null(options.ProductSlug);
        }
    }
}
