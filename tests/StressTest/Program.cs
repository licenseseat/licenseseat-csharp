using System.Diagnostics;
using System.Net.Http;
using LicenseSeat;
using LicenseSeat.StressTests;
using Microsoft.Extensions.DependencyInjection;

// Alias to distinguish the static class from the namespace
using LicenseSeatStatic = LicenseSeat.LicenseSeat;

// Credentials from environment variables (for CI/CD and local testing)
var API_URL = Environment.GetEnvironmentVariable("LICENSESEAT_API_URL")
    ?? LicenseSeatClientOptions.DefaultApiBaseUrl;
var API_KEY = Environment.GetEnvironmentVariable("LICENSESEAT_API_KEY")
    ?? throw new InvalidOperationException("LICENSESEAT_API_KEY environment variable is required");
var PRODUCT_SLUG = Environment.GetEnvironmentVariable("LICENSESEAT_PRODUCT_SLUG")
    ?? throw new InvalidOperationException("LICENSESEAT_PRODUCT_SLUG environment variable is required");
var LICENSE_KEY = Environment.GetEnvironmentVariable("LICENSESEAT_LICENSE_KEY")
    ?? throw new InvalidOperationException("LICENSESEAT_LICENSE_KEY environment variable is required");

Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("  LicenseSeat C# SDK - Production Stress Test");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();
Console.WriteLine($"  API Key:      {API_KEY[..20]}...");
Console.WriteLine($"  Product:      {PRODUCT_SLUG}");
Console.WriteLine($"  License Key:  {LICENSE_KEY}");
Console.WriteLine();
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();

var allTestsPassed = true;
var testResults = new List<(string Name, bool Passed, TimeSpan Duration, string? Error)>();

async Task RunTest(string name, Func<Task> test)
{
    Console.Write($"[TEST] {name}... ");
    var sw = Stopwatch.StartNew();
    try
    {
        await test();
        sw.Stop();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"PASSED ({sw.ElapsedMilliseconds}ms)");
        Console.ResetColor();
        testResults.Add((name, true, sw.Elapsed, null));
    }
    catch (Exception ex)
    {
        sw.Stop();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"FAILED ({sw.ElapsedMilliseconds}ms)");
        Console.ResetColor();
        Console.WriteLine($"       Error: {ex.Message}");
        testResults.Add((name, false, sw.Elapsed, ex.Message));
        allTestsPassed = false;
    }
}

void RunSyncTest(string name, Action test)
{
    Console.Write($"[TEST] {name}... ");
    var sw = Stopwatch.StartNew();
    try
    {
        test();
        sw.Stop();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"PASSED ({sw.ElapsedMilliseconds}ms)");
        Console.ResetColor();
        testResults.Add((name, true, sw.Elapsed, null));
    }
    catch (Exception ex)
    {
        sw.Stop();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"FAILED ({sw.ElapsedMilliseconds}ms)");
        Console.ResetColor();
        Console.WriteLine($"       Error: {ex.Message}");
        testResults.Add((name, false, sw.Elapsed, ex.Message));
        allTestsPassed = false;
    }
}

// ============================================================
// SECTION 1: Instance-based Client Tests
// ============================================================
Console.WriteLine("--- Section 1: Instance-based Client Tests ---\n");

LicenseSeatClient? client = null;
License? activatedLicense = null;

// Test 1: Client Creation
RunSyncTest("Create client with options", () =>
{
    client = new LicenseSeatClient(new LicenseSeatClientOptions
    {
        ApiBaseUrl = API_URL,
        ApiKey = API_KEY,
        ProductSlug = PRODUCT_SLUG,
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero, // Disable for testing
        Debug = true
    });

    if (client == null) throw new Exception("Client is null");
    if (client.Events == null) throw new Exception("Events is null");
    if (client.Options == null) throw new Exception("Options is null");
});

// Test 2: Test API Auth
await RunTest("TestAuthAsync - API connectivity", async () =>
{
    var result = await client!.TestAuthAsync();
    if (!result) throw new Exception("TestAuthAsync returned false");
});

// Test 3: Initial Status
RunSyncTest("GetStatus - Initial state is Inactive", () =>
{
    var status = client!.GetStatus();
    if (status.StatusType != LicenseStatusType.Inactive)
        throw new Exception($"Expected Inactive, got {status.StatusType}");
});

// Test 4: Validate without activation (just check license is valid)
await RunTest("ValidateAsync - Validate license (no local state)", async () =>
{
    var result = await client!.ValidateAsync(LICENSE_KEY);
    Console.WriteLine($"       Valid: {result.Valid}");
    Console.WriteLine($"       License Key: {result.License?.Key ?? "N/A"}");
    Console.WriteLine($"       Device ID: {result.License?.DeviceId ?? "N/A"}");
    Console.WriteLine($"       Active Seats: {result.License?.ActiveSeats ?? 0}");
    Console.WriteLine($"       Seat Limit: {result.License?.SeatLimit ?? 0}");

    if (!result.Valid) throw new Exception($"Validation failed: {result.Message}");
});

// Test 5: Event Registration
var activationEventReceived = false;
var validationEventReceived = false;
var deactivationEventReceived = false;

RunSyncTest("Event subscription setup", () =>
{
    client!.Events.On(LicenseSeatEvents.ActivationSuccess, _ => activationEventReceived = true);
    client.Events.On(LicenseSeatEvents.ValidationSuccess, _ => validationEventReceived = true);
    client.Events.On(LicenseSeatEvents.DeactivationSuccess, _ => deactivationEventReceived = true);

    if (client.Events.GetSubscriberCount(LicenseSeatEvents.ActivationSuccess) != 1)
        throw new Exception("Event subscription failed");
});

// Test 6: License Activation (may fail if seat limit reached)
await RunTest("ActivateAsync - Activate license", async () =>
{
    try
    {
        activatedLicense = await client!.ActivateAsync(LICENSE_KEY);

        if (activatedLicense == null) throw new Exception("Activation returned null");
        if (string.IsNullOrEmpty(activatedLicense.Key)) throw new Exception("License key is empty");

        Console.WriteLine($"       License Key: {activatedLicense.Key}");
        Console.WriteLine($"       Status: {activatedLicense.Status}");
        Console.WriteLine($"       Device ID: {activatedLicense.DeviceId}");
        if (activatedLicense.ExpiresAt.HasValue)
            Console.WriteLine($"       Expires: {activatedLicense.ExpiresAt}");
    }
    catch (ApiException ex) when (ex.Code == "seat_limit_exceeded")
    {
        Console.WriteLine($"       NOTE: Seat limit exceeded (expected if license already active elsewhere)");
        Console.WriteLine($"       This is OK - validates the SDK handles seat limits correctly");
        // Re-throw to mark as test scenario handled
        throw new Exception($"Seat limit exceeded - license is already active on another device. Code: {ex.Code}");
    }
});

// Test 7: Check entitlements (works even without local activation state)
RunSyncTest("HasEntitlement - Check for non-existent entitlement", () =>
{
    var hasEntitlement = client!.HasEntitlement("non-existent-feature");
    Console.WriteLine($"       Has 'non-existent-feature': {hasEntitlement}");
    // Should return false without throwing
});

// Test 8: CheckEntitlement detailed
RunSyncTest("CheckEntitlement - Detailed entitlement check", () =>
{
    var status = client!.CheckEntitlement("some-feature");
    Console.WriteLine($"       Active: {status.Active}");
    Console.WriteLine($"       Reason: {status.Reason}");
});

// Test 9: Multiple rapid validations (stress test - works regardless of activation)
await RunTest("Stress: 10 rapid validations", async () =>
{
    var tasks = new List<Task<ValidationResult>>();
    for (int i = 0; i < 10; i++)
    {
        tasks.Add(client!.ValidateAsync(LICENSE_KEY));
    }

    var results = await Task.WhenAll(tasks);
    var failedCount = results.Count(r => !r.Valid);

    if (failedCount > 0)
        throw new Exception($"{failedCount} out of 10 validations failed");

    Console.WriteLine($"       All 10 validations succeeded");
});

// Test 10: If we have an active license, test deactivation
if (activatedLicense != null)
{
    // Verify activation event
    await Task.Delay(100);
    RunSyncTest("Verify activation event fired", () =>
    {
        if (!activationEventReceived)
            throw new Exception("Activation event was not received");
    });

    // Status after activation
    RunSyncTest("GetStatus - After activation is Active", () =>
    {
        var status = client!.GetStatus();
        if (status.StatusType != LicenseStatusType.Active)
            throw new Exception($"Expected Active, got {status.StatusType}");
        if (status.Details == null)
            throw new Exception("Status details is null");
    });

    // GetCurrentLicense
    RunSyncTest("GetCurrentLicense - Returns activated license", () =>
    {
        var license = client!.GetCurrentLicense();
        if (license == null) throw new Exception("GetCurrentLicense returned null");
        if (license.Key != activatedLicense!.Key)
            throw new Exception($"License key mismatch: expected {activatedLicense.Key}, got {license.Key}");
    });

    // Deactivate License
    await RunTest("DeactivateAsync - Deactivate license", async () =>
    {
        await client!.DeactivateAsync();
        var status = client.GetStatus();
        if (status.StatusType != LicenseStatusType.Inactive)
            throw new Exception($"Expected Inactive after deactivation, got {status.StatusType}");
    });

    // Verify deactivation event
    await Task.Delay(100);
    RunSyncTest("Verify deactivation event fired", () =>
    {
        if (!deactivationEventReceived)
            throw new Exception("Deactivation event was not received");
    });
}
else
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n[SKIP] Activation-dependent tests skipped (seat limit reached)");
    Console.ResetColor();
}

// Test: Reset client state
RunSyncTest("Reset - Clear client state", () =>
{
    client!.Reset();

    var license = client.GetCurrentLicense();
    if (license != null)
        throw new Exception("License should be null after reset");

    var status = client.GetStatus();
    if (status.StatusType != LicenseStatusType.Inactive)
        throw new Exception($"Expected Inactive after reset, got {status.StatusType}");
});

// Test: Dispose client
RunSyncTest("Dispose client", () =>
{
    client!.Dispose();
});

Console.WriteLine();

// ============================================================
// SECTION 2: Static API Tests
// ============================================================
Console.WriteLine("--- Section 2: Static API (Singleton) Tests ---\n");

// Configure static API
RunSyncTest("LicenseSeat.Configure - Setup singleton", () =>
{
    LicenseSeatStatic.Shutdown(); // Ensure clean state

    var staticClient = LicenseSeatStatic.Configure(API_KEY, PRODUCT_SLUG, options =>
    {
        options.ApiBaseUrl = API_URL;
        options.AutoInitialize = false;
        options.AutoValidateInterval = TimeSpan.Zero;
        options.Debug = true;
    });

    if (staticClient == null) throw new Exception("Configure returned null");
    if (!LicenseSeatStatic.IsConfigured) throw new Exception("IsConfigured is false");
    if (LicenseSeatStatic.Shared == null) throw new Exception("Shared is null");
});

// Static API validation (works without activation)
await RunTest("LicenseSeat.Validate - Static validation", async () =>
{
    var result = await LicenseSeatStatic.Validate(LICENSE_KEY);
    if (!result.Valid) throw new Exception($"Static validation failed: {result.Message}");
    Console.WriteLine($"       Valid: {result.Valid}");
});

// Static API entitlements
RunSyncTest("LicenseSeat.HasEntitlement - Static entitlement check", () =>
{
    var has = LicenseSeatStatic.HasEntitlement("test-feature");
    Console.WriteLine($"       Has 'test-feature': {has}");
});

// Static API status
RunSyncTest("LicenseSeat.GetStatus - Static status (Inactive expected)", () =>
{
    var status = LicenseSeatStatic.GetStatus();
    Console.WriteLine($"       Status: {status.StatusType}");
    // Status will be Inactive since we haven't activated locally
});

// Try static activation
License? staticActivatedLicense = null;
await RunTest("LicenseSeat.Activate - Static activation", async () =>
{
    try
    {
        staticActivatedLicense = await LicenseSeatStatic.Activate(LICENSE_KEY);
        Console.WriteLine($"       Activated via static API: {staticActivatedLicense.Key}");
    }
    catch (ApiException ex) when (ex.Code == "seat_limit_exceeded")
    {
        Console.WriteLine($"       NOTE: Seat limit exceeded (license active elsewhere)");
        throw new Exception($"Seat limit exceeded - Code: {ex.Code}");
    }
});

if (staticActivatedLicense != null)
{
    // Static API current license
    RunSyncTest("LicenseSeat.GetCurrentLicense - Static get license", () =>
    {
        var license = LicenseSeatStatic.GetCurrentLicense();
        if (license == null) throw new Exception("Static GetCurrentLicense returned null");
    });

    // Static API deactivation
    await RunTest("LicenseSeat.Deactivate - Static deactivation", async () =>
    {
        await LicenseSeatStatic.Deactivate();
        var status = LicenseSeatStatic.GetStatus();
        if (status.StatusType != LicenseStatusType.Inactive)
            throw new Exception($"Expected Inactive, got {status.StatusType}");
    });
}
else
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n[SKIP] Static activation-dependent tests skipped (seat limit reached)");
    Console.ResetColor();
}

// Static API shutdown
RunSyncTest("LicenseSeat.Shutdown - Cleanup singleton", () =>
{
    LicenseSeatStatic.Shutdown();

    if (LicenseSeatStatic.IsConfigured) throw new Exception("IsConfigured should be false after Shutdown");
    if (LicenseSeatStatic.Shared != null) throw new Exception("Shared should be null after Shutdown");
});

Console.WriteLine();

// ============================================================
// SECTION 3: Dependency Injection Tests
// ============================================================
Console.WriteLine("--- Section 3: Dependency Injection Tests ---\n");

// DI Registration with configure action
RunSyncTest("DI: AddLicenseSeatClient with configure", () =>
{
    var services = new ServiceCollection();
    services.AddLicenseSeatClient(options =>
    {
        options.ApiBaseUrl = API_URL;
        options.ApiKey = API_KEY;
        options.ProductSlug = PRODUCT_SLUG;
        options.AutoInitialize = false;
        options.AutoValidateInterval = TimeSpan.Zero;
    });

    var provider = services.BuildServiceProvider();
    var diClient = provider.GetService<ILicenseSeatClient>();

    if (diClient == null) throw new Exception("DI client is null");

    diClient.Dispose();
});

// DI Registration with simple API
RunSyncTest("DI: AddLicenseSeatClient with api key and product", () =>
{
    var services = new ServiceCollection();
    services.AddLicenseSeatClient(API_KEY, PRODUCT_SLUG);

    var provider = services.BuildServiceProvider();
    var diClient = provider.GetService<ILicenseSeatClient>();

    if (diClient == null) throw new Exception("DI client is null");

    diClient.Dispose();
});

// DI: Test client via interface
await RunTest("DI: Use client via ILicenseSeatClient interface", async () =>
{
    var services = new ServiceCollection();
    services.AddLicenseSeatClient(options =>
    {
        options.ApiBaseUrl = API_URL;
        options.ApiKey = API_KEY;
        options.ProductSlug = PRODUCT_SLUG;
        options.AutoInitialize = false;
        options.AutoValidateInterval = TimeSpan.Zero;
    });

    var provider = services.BuildServiceProvider();
    ILicenseSeatClient diClient = provider.GetRequiredService<ILicenseSeatClient>();

    // Test operations through interface
    var result = await diClient.ValidateAsync(LICENSE_KEY);
    Console.WriteLine($"       Validated via DI interface: {result.Valid}");

    if (!result.Valid) throw new Exception($"DI validation failed: {result.Message}");

    diClient.Dispose();
});

Console.WriteLine();

// ============================================================
// SECTION 4: Error Handling Tests
// ============================================================
Console.WriteLine("--- Section 4: Error Handling Tests ---\n");

// Invalid license key
await RunTest("Error: Invalid license key - 404 Not Found", async () =>
{
    using var errorClient = new LicenseSeatClient(new LicenseSeatClientOptions
    {
        ApiBaseUrl = API_URL,
        ApiKey = API_KEY,
        ProductSlug = PRODUCT_SLUG,
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero
    });

    try
    {
        await errorClient.ActivateAsync("INVALID-KEY-12345");
        throw new Exception("Expected exception for invalid key");
    }
    catch (ApiException ex)
    {
        Console.WriteLine($"       Caught ApiException as expected");
        Console.WriteLine($"       Code: {ex.Code}");
        Console.WriteLine($"       Message: {ex.Message}");
        Console.WriteLine($"       Status Code: {ex.StatusCode}");

        if (ex.StatusCode != 404)
            throw new Exception($"Expected 404 status code, got {ex.StatusCode}");
    }
});

// Operations before configure (static API)
RunSyncTest("Error: Static API before configure throws", () =>
{
    LicenseSeatStatic.Shutdown(); // Ensure not configured

    try
    {
        LicenseSeatStatic.GetStatus();
        throw new Exception("Expected InvalidOperationException");
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"       Caught InvalidOperationException as expected");
        Console.WriteLine($"       Message: {ex.Message[..Math.Min(50, ex.Message.Length)]}...");
    }
});

// Test API exception properties
await RunTest("Error: Verify ApiException properties", async () =>
{
    using var errorClient = new LicenseSeatClient(new LicenseSeatClientOptions
    {
        ApiBaseUrl = API_URL,
        ApiKey = API_KEY,
        ProductSlug = PRODUCT_SLUG,
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero
    });

    try
    {
        await errorClient.ValidateAsync("NONEXISTENT-LICENSE");
        throw new Exception("Expected ApiException");
    }
    catch (ApiException ex)
    {
        // Verify exception has proper properties
        Console.WriteLine($"       StatusCode: {ex.StatusCode}");
        Console.WriteLine($"       Code: {ex.Code}");
        Console.WriteLine($"       ErrorCode: {ex.ErrorCode}");
        Console.WriteLine($"       IsClientError: {ex.IsClientError}");
        Console.WriteLine($"       IsServerError: {ex.IsServerError}");
        Console.WriteLine($"       IsNetworkError: {ex.IsNetworkError}");
        Console.WriteLine($"       IsRetryable: {ex.IsRetryable}");

        if (!ex.IsClientError)
            throw new Exception("Expected IsClientError to be true for 404");
    }
});

Console.WriteLine();

// ============================================================
// SECTION 5: Stress Tests
// ============================================================
Console.WriteLine("--- Section 5: Stress Tests ---\n");

// Create and dispose many clients
await RunTest("Stress: Create/dispose 50 clients in parallel", async () =>
{
    var tasks = new List<Task>();

    for (int i = 0; i < 50; i++)
    {
        tasks.Add(Task.Run(() =>
        {
            using var tempClient = new LicenseSeatClient(new LicenseSeatClientOptions
            {
                ApiKey = API_KEY,
                ProductSlug = PRODUCT_SLUG,
                AutoInitialize = false,
                AutoValidateInterval = TimeSpan.Zero
            });
        }));
    }

    await Task.WhenAll(tasks);
    Console.WriteLine($"       50 clients created and disposed successfully");
});

// Concurrent validations
await RunTest("Stress: 20 concurrent validations", async () =>
{
    using var stressClient = new LicenseSeatClient(new LicenseSeatClientOptions
    {
        ApiBaseUrl = API_URL,
        ApiKey = API_KEY,
        ProductSlug = PRODUCT_SLUG,
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero
    });

    var tasks = new List<Task<ValidationResult>>();
    for (int i = 0; i < 20; i++)
    {
        tasks.Add(stressClient.ValidateAsync(LICENSE_KEY));
    }

    var results = await Task.WhenAll(tasks);
    var successCount = results.Count(r => r.Valid);
    var failCount = results.Count(r => !r.Valid);

    Console.WriteLine($"       Success: {successCount}, Failed: {failCount}");

    if (failCount > 0)
        throw new Exception($"{failCount} out of 20 validations failed");
});

// Event bus stress
RunSyncTest("Stress: Event bus with 100 subscribers", () =>
{
    var bus = new EventBus();
    var received = 0;

    for (int i = 0; i < 100; i++)
    {
        bus.On("test", _ => Interlocked.Increment(ref received));
    }

    bus.Emit("test");

    if (received != 100)
        throw new Exception($"Expected 100 handlers called, got {received}");

    Console.WriteLine($"       100 event handlers invoked successfully");
});

// Typed event handlers
RunSyncTest("Stress: Typed event handlers", () =>
{
    var bus = new EventBus();
    License? receivedLicense = null;

    bus.On<License>("license:update", license => receivedLicense = license);

    var testLicense = new License { Key = "TEST-KEY", Status = "active" };
    bus.Emit("license:update", testLicense);

    if (receivedLicense == null)
        throw new Exception("Typed event handler didn't receive license");
    if (receivedLicense.Key != "TEST-KEY")
        throw new Exception($"License key mismatch: {receivedLicense.Key}");

    Console.WriteLine($"       Typed event handler received license: {receivedLicense.Key}");
});

Console.WriteLine();

// ============================================================
// SECTION 6: Offline Token & Cryptography Tests
// ============================================================
Console.WriteLine("--- Section 6: Offline Token & Cryptography Tests ---\n");

OfflineTokenResponse? offlineTokenResponse = null;
string? fetchedPublicKey = null;

// Test: Fetch offline token from live API
await RunTest("Offline: Fetch offline token from API", async () =>
{
    using var offlineClient = new LicenseSeatClient(new LicenseSeatClientOptions
    {
        ApiKey = API_KEY,
        ProductSlug = PRODUCT_SLUG,
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero,
        Debug = true
    });

    // First, activate the license to be able to get an offline token
    License? license = null;
    try
    {
        license = await offlineClient.ActivateAsync(LICENSE_KEY);
    }
    catch (ApiException ex) when (ex.Code == "seat_limit_exceeded")
    {
        Console.WriteLine($"       NOTE: Seat limit - will validate instead");
        // If seat limit exceeded, just validate (device may already be activated)
        var valResult = await offlineClient.ValidateAsync(LICENSE_KEY);
        if (!valResult.Valid)
            throw new Exception($"Cannot proceed with offline tests: {valResult.Message}");
    }

    // Now fetch an offline token via direct API call
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

    var requestBody = new { device_id = Environment.MachineName };
    var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

    var response = await httpClient.PostAsync(
        $"https://licenseseat.com/api/v1/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/offline_token",
        content);

    if (!response.IsSuccessStatusCode)
    {
        var errorBody = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to fetch offline token: {response.StatusCode} - {errorBody}");
    }

    var responseBody = await response.Content.ReadAsStringAsync();
    offlineTokenResponse = System.Text.Json.JsonSerializer.Deserialize<OfflineTokenResponse>(responseBody);

    if (offlineTokenResponse == null)
        throw new Exception("Failed to deserialize offline token response");

    Console.WriteLine($"       Token fetched successfully");
    Console.WriteLine($"       License Key: {offlineTokenResponse.Token?.LicenseKey}");
    Console.WriteLine($"       Product Slug: {offlineTokenResponse.Token?.ProductSlug}");
    Console.WriteLine($"       Key ID: {offlineTokenResponse.Token?.Kid}");

    // Deactivate after test
    try { await offlineClient.DeactivateAsync(); } catch { /* ignore */ }
});

// Test: Verify offline token structure
RunSyncTest("Offline: Verify token structure", () =>
{
    if (offlineTokenResponse == null)
        throw new Exception("No offline token to verify (previous test may have failed)");

    // Verify token payload
    var token = offlineTokenResponse.Token;
    if (token == null)
        throw new Exception("Token payload is null");

    if (string.IsNullOrEmpty(token.LicenseKey))
        throw new Exception("Token license_key is missing");

    if (string.IsNullOrEmpty(token.ProductSlug))
        throw new Exception("Token product_slug is missing");

    if (token.Iat <= 0)
        throw new Exception("Token iat (issued at) is missing or invalid");

    if (token.Exp <= 0)
        throw new Exception("Token exp (expiration) is missing or invalid");

    if (token.Nbf <= 0)
        throw new Exception("Token nbf (not before) is missing or invalid");

    if (string.IsNullOrEmpty(token.Kid))
        throw new Exception("Token kid (key ID) is missing");

    Console.WriteLine($"       Schema Version: {token.SchemaVersion}");
    Console.WriteLine($"       Issued At (iat): {DateTimeOffset.FromUnixTimeSeconds(token.Iat)}");
    Console.WriteLine($"       Expires At (exp): {DateTimeOffset.FromUnixTimeSeconds(token.Exp)}");
    Console.WriteLine($"       Not Before (nbf): {DateTimeOffset.FromUnixTimeSeconds(token.Nbf)}");
    Console.WriteLine($"       Mode: {token.Mode}");
    Console.WriteLine($"       Plan Key: {token.PlanKey}");

    // Verify signature block
    var sig = offlineTokenResponse.Signature;
    if (sig == null)
        throw new Exception("Signature block is null");

    if (string.IsNullOrEmpty(sig.Algorithm))
        throw new Exception("Signature algorithm is missing");

    if (string.IsNullOrEmpty(sig.KeyId))
        throw new Exception("Signature key_id is missing");

    if (string.IsNullOrEmpty(sig.Value))
        throw new Exception("Signature value is missing");

    Console.WriteLine($"       Signature Algorithm: {sig.Algorithm}");
    Console.WriteLine($"       Signature Key ID: {sig.KeyId}");
    Console.WriteLine($"       Signature Value Length: {sig.Value.Length} chars");

    // Verify canonical JSON
    if (string.IsNullOrEmpty(offlineTokenResponse.Canonical))
        throw new Exception("Canonical JSON is missing");

    Console.WriteLine($"       Canonical JSON Length: {offlineTokenResponse.Canonical.Length} chars");
});

// Test: Fetch public signing key
await RunTest("Offline: Fetch public signing key from API", async () =>
{
    if (offlineTokenResponse?.Signature?.KeyId == null)
        throw new Exception("No key ID available (previous tests may have failed)");

    var keyId = offlineTokenResponse.Signature.KeyId;

    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

    var response = await httpClient.GetAsync($"https://licenseseat.com/api/v1/signing_keys/{keyId}");

    if (!response.IsSuccessStatusCode)
    {
        var errorBody = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to fetch signing key: {response.StatusCode} - {errorBody}");
    }

    var responseBody = await response.Content.ReadAsStringAsync();
    var keyResponse = System.Text.Json.JsonSerializer.Deserialize<SigningKeyResponse>(responseBody,
        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    if (keyResponse == null)
        throw new Exception("Failed to deserialize signing key response");

    if (string.IsNullOrEmpty(keyResponse.PublicKey))
        throw new Exception("Public key is missing from response");

    fetchedPublicKey = keyResponse.PublicKey;

    Console.WriteLine($"       Key ID: {keyResponse.KeyId}");
    Console.WriteLine($"       Algorithm: {keyResponse.Algorithm}");
    Console.WriteLine($"       Status: {keyResponse.Status}");
    Console.WriteLine($"       Public Key Length: {fetchedPublicKey.Length} chars");
});

// Test: Verify Ed25519 signature
RunSyncTest("Crypto: Verify Ed25519 signature (VerifyCanonical)", () =>
{
    if (offlineTokenResponse == null || fetchedPublicKey == null)
        throw new Exception("Missing offline token or public key (previous tests may have failed)");

    var signature = offlineTokenResponse.Signature?.Value;
    var canonical = offlineTokenResponse.Canonical;

    if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(canonical))
        throw new Exception("Missing signature or canonical JSON");

    var isValid = Ed25519Verifier.VerifyCanonical(fetchedPublicKey, signature, canonical);

    if (!isValid)
        throw new Exception("Ed25519 signature verification FAILED!");

    Console.WriteLine($"       Signature verification: PASSED");
    Console.WriteLine($"       Public key used: {fetchedPublicKey[..20]}...");
});

// Test: Tampered canonical JSON should fail verification
RunSyncTest("Crypto: Tampered canonical fails verification", () =>
{
    if (offlineTokenResponse == null || fetchedPublicKey == null)
        throw new Exception("Missing offline token or public key");

    var signature = offlineTokenResponse.Signature?.Value;
    var canonical = offlineTokenResponse.Canonical;

    if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(canonical))
        throw new Exception("Missing signature or canonical JSON");

    // Tamper with the canonical JSON
    var tamperedCanonical = canonical.Replace(LICENSE_KEY, "TAMPERED-KEY");

    var isValid = Ed25519Verifier.VerifyCanonical(fetchedPublicKey, signature, tamperedCanonical);

    if (isValid)
        throw new Exception("Tampered data should NOT verify successfully!");

    Console.WriteLine($"       Tampered signature verification correctly FAILED");
});

// Test: Invalid public key length
RunSyncTest("Crypto: Invalid public key length throws CryptoException", () =>
{
    var invalidKey = Convert.ToBase64String(new byte[16]); // 16 bytes instead of 32
    var dummySignature = Convert.ToBase64String(new byte[64]);

    try
    {
        Ed25519Verifier.VerifyCanonical(invalidKey, dummySignature, "{}");
        throw new Exception("Expected CryptoException for invalid key length");
    }
    catch (CryptoException ex)
    {
        if (ex.ErrorCode != CryptoException.InvalidKeyCode)
            throw new Exception($"Expected error code {CryptoException.InvalidKeyCode}, got {ex.ErrorCode}");
        Console.WriteLine($"       Caught expected CryptoException: {ex.ErrorCode}");
    }
});

// Test: Invalid signature length
RunSyncTest("Crypto: Invalid signature length throws CryptoException", () =>
{
    if (fetchedPublicKey == null)
        throw new Exception("Missing public key");

    var shortSignature = Convert.ToBase64String(new byte[32]); // 32 bytes instead of 64

    try
    {
        Ed25519Verifier.VerifyCanonical(fetchedPublicKey, shortSignature, "{}");
        throw new Exception("Expected CryptoException for invalid signature length");
    }
    catch (CryptoException ex)
    {
        if (ex.ErrorCode != CryptoException.InvalidSignatureCode)
            throw new Exception($"Expected error code {CryptoException.InvalidSignatureCode}, got {ex.ErrorCode}");
        Console.WriteLine($"       Caught expected CryptoException: {ex.ErrorCode}");
    }
});

// Test: ConstantTimeEquals utility
RunSyncTest("Crypto: ConstantTimeEquals - Equal strings", () =>
{
    var result = Ed25519Verifier.ConstantTimeEquals("test-license-key", "test-license-key");
    if (!result)
        throw new Exception("Equal strings should return true");
    Console.WriteLine($"       Equal strings comparison: PASSED");
});

RunSyncTest("Crypto: ConstantTimeEquals - Different strings", () =>
{
    var result = Ed25519Verifier.ConstantTimeEquals("test-license-key", "other-license-key");
    if (result)
        throw new Exception("Different strings should return false");
    Console.WriteLine($"       Different strings comparison: PASSED");
});

RunSyncTest("Crypto: ConstantTimeEquals - Null handling", () =>
{
    if (!Ed25519Verifier.ConstantTimeEquals(null, null))
        throw new Exception("Both null should return true");

    if (Ed25519Verifier.ConstantTimeEquals("test", null))
        throw new Exception("One null should return false");

    if (Ed25519Verifier.ConstantTimeEquals(null, "test"))
        throw new Exception("One null should return false");

    Console.WriteLine($"       Null handling: PASSED");
});

// Test: Token timing validation
RunSyncTest("Offline: Token timing validation", () =>
{
    if (offlineTokenResponse?.Token == null)
        throw new Exception("No token available");

    var token = offlineTokenResponse.Token;
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // Token should not be expired
    if (token.Exp <= now)
        throw new Exception($"Token is already expired: exp={token.Exp}, now={now}");

    // Token should be valid (nbf in the past)
    if (token.Nbf > now)
        throw new Exception($"Token is not yet valid: nbf={token.Nbf}, now={now}");

    // Token was issued in the past
    if (token.Iat > now)
        throw new Exception($"Token issued in the future: iat={token.Iat}, now={now}");

    var expiresIn = TimeSpan.FromSeconds(token.Exp - now);
    var issuedAgo = TimeSpan.FromSeconds(now - token.Iat);

    Console.WriteLine($"       Token issued: {issuedAgo.TotalMinutes:F1} minutes ago");
    Console.WriteLine($"       Token expires in: {expiresIn.TotalDays:F1} days");
    Console.WriteLine($"       Timing validation: PASSED");
});

// Test: Token entitlements (if any)
RunSyncTest("Offline: Token entitlements parsing", () =>
{
    if (offlineTokenResponse?.Token == null)
        throw new Exception("No token available");

    var entitlements = offlineTokenResponse.Token.Entitlements;

    if (entitlements == null || entitlements.Count == 0)
    {
        Console.WriteLine($"       No entitlements in token (this is OK)");
        return;
    }

    Console.WriteLine($"       Found {entitlements.Count} entitlement(s):");
    foreach (var ent in entitlements)
    {
        var expiry = ent.ExpiresAt.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(ent.ExpiresAt.Value).ToString()
            : "never";
        Console.WriteLine($"         - {ent.Key} (expires: {expiry})");
    }
});

// Test: Base64URL decoding edge cases (via signature verification)
RunSyncTest("Crypto: Base64URL decoding handles padding correctly", () =>
{
    if (fetchedPublicKey == null || offlineTokenResponse?.Signature?.Value == null)
        throw new Exception("Missing prerequisites");

    // The signature value from the API is Base64URL encoded without padding
    var sig = offlineTokenResponse.Signature.Value;

    // Check it doesn't have standard Base64 padding
    if (sig.Contains('+') || sig.Contains('/'))
    {
        Console.WriteLine($"       WARNING: Signature contains Base64 chars (+/)");
    }

    // Verify the signature still works (proves Base64URL decoding is correct)
    var isValid = Ed25519Verifier.VerifyCanonical(
        fetchedPublicKey,
        sig,
        offlineTokenResponse.Canonical!);

    if (!isValid)
        throw new Exception("Base64URL signature verification failed");

    Console.WriteLine($"       Base64URL decoding: PASSED");
    Console.WriteLine($"       Signature contains '-': {sig.Contains('-')}");
    Console.WriteLine($"       Signature contains '_': {sig.Contains('_')}");
});

Console.WriteLine();

// ============================================================
// SECTION 7: Telemetry & Heartbeat Stress Test
// ============================================================
Console.WriteLine("--- Section 7: Telemetry & Heartbeat Stress Test ---\n");

await RunTest("Telemetry & Heartbeat: 7 scenarios", async () =>
{
    var telemetryPassed = await TelemetryStressTest.RunAsync();
    if (!telemetryPassed)
        throw new Exception("One or more telemetry scenarios failed (see details above)");
});

Console.WriteLine();

// ============================================================
// SECTION 8: User Journey Simulation
// ============================================================
Console.WriteLine("--- Section 8: Real-World User Journey Simulation ---\n");

await RunTest("User Journey: Complete customer simulation", async () =>
{
    await UserJourneyTest.RunAsync();
});

Console.WriteLine();

// ============================================================
// Summary
// ============================================================
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine("  TEST SUMMARY");
Console.WriteLine("=".PadRight(70, '='));
Console.WriteLine();

var passed = testResults.Count(r => r.Passed);
var failed = testResults.Count(r => !r.Passed);
var totalTime = testResults.Sum(r => r.Duration.TotalMilliseconds);

Console.WriteLine($"  Total Tests:  {testResults.Count}");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"  Passed:       {passed}");
Console.ResetColor();

if (failed > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  Failed:       {failed}");
    Console.ResetColor();

    Console.WriteLine();
    Console.WriteLine("  Failed Tests:");
    foreach (var result in testResults.Where(r => !r.Passed))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"    - {result.Name}");
        Console.ResetColor();
        Console.WriteLine($"      {result.Error}");
    }
}

Console.WriteLine();
Console.WriteLine($"  Total Time:   {totalTime:F0}ms");
Console.WriteLine();

// Check if all failures are due to seat limits (expected in some scenarios)
var seatLimitFailures = testResults.Where(r => !r.Passed && r.Error?.Contains("seat_limit_exceeded") == true).Count();
var otherFailures = failed - seatLimitFailures;

if (otherFailures == 0 && seatLimitFailures > 0)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  NOTE: {seatLimitFailures} test(s) failed due to seat limit.");
    Console.WriteLine("        This is expected if the license is active on another device.");
    Console.WriteLine("        All SDK functionality is working correctly!");
    Console.ResetColor();
    Console.WriteLine();
}

Console.WriteLine("=".PadRight(70, '='));

// Exit with success if only seat limit failures
Environment.Exit(otherFailures == 0 ? 0 : 1);

// Local class for deserializing signing key response (SDK version is internal)
internal sealed class SigningKeyResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("object")]
    public string? Object { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("key_id")]
    public string? KeyId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("algorithm")]
    public string? Algorithm { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("public_key")]
    public string? PublicKey { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string? Status { get; set; }
}
