using System.Text.Json;

namespace LicenseSeat.Tests;

internal static class TestResponses
{
    internal static string Activation(
        string licenseKey = "TEST-KEY",
        string fingerprint = "device-123",
        string productSlug = "test-product")
    {
        return JsonSerializer.Serialize(new
        {
            @object = "activation",
            id = "123",
            fingerprint,
            license_key = licenseKey,
            activated_at = "2024-01-01T00:00:00Z",
            license = ActiveLicense(licenseKey, productSlug)
        });
    }

    internal static string ValidValidation(
        string licenseKey = "TEST-KEY",
        string productSlug = "test-product")
    {
        return JsonSerializer.Serialize(new
        {
            @object = "validation_result",
            valid = true,
            license = ActiveLicense(licenseKey, productSlug)
        });
    }

    internal static string InvalidValidation(
        string licenseKey = "EXPIRED-KEY",
        string productSlug = "test-product")
    {
        return JsonSerializer.Serialize(new
        {
            @object = "validation_result",
            valid = false,
            code = "expired",
            message = "License has expired",
            license = new
            {
                @object = "license",
                key = licenseKey,
                status = "expired",
                starts_at = "2023-01-01T00:00:00Z",
                expires_at = "2024-01-01T00:00:00Z",
                mode = "hardware_locked",
                plan_key = "pro",
                active_seats = 0,
                active_entitlements = Array.Empty<object>(),
                product = new { @object = "product", slug = productSlug, name = "Test Product" }
            }
        });
    }

    internal static string Heartbeat(
        string licenseKey = "TEST-KEY",
        string productSlug = "test-product")
    {
        return JsonSerializer.Serialize(new
        {
            @object = "heartbeat",
            received_at = "2024-01-01T00:00:00Z",
            license = ActiveLicense(licenseKey, productSlug)
        });
    }

    internal static string Deactivation()
    {
        return "{\"object\":\"deactivation\",\"activation_id\":\"123\",\"deactivated_at\":\"2024-01-01T00:00:00Z\"}";
    }

    internal static string Authentication()
    {
        return "{\"object\":\"authentication\",\"authenticated\":true,\"scope\":\"licenses:validate\",\"api_version\":\"v1\",\"timestamp\":\"2024-01-01T00:00:00Z\"}";
    }

    private static object ActiveLicense(string licenseKey, string productSlug)
    {
        return new
        {
            @object = "license",
            key = licenseKey,
            status = "active",
            starts_at = "2024-01-01T00:00:00Z",
            mode = "hardware_locked",
            plan_key = "pro",
            active_seats = 1,
            active_entitlements = Array.Empty<object>(),
            product = new { @object = "product", slug = productSlug, name = "Test Product" }
        };
    }
}
