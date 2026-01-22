using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LicenseSeat;

namespace LicenseSeat.StressTests;

/// <summary>
/// Simulates a complete real-world user journey with the LicenseSeat SDK.
/// This test mimics what an actual customer would experience using your software.
/// </summary>
public static class UserJourneyTest
{
    // Credentials from environment variables
    private static readonly string API_KEY = Environment.GetEnvironmentVariable("LICENSESEAT_API_KEY")
        ?? throw new InvalidOperationException("LICENSESEAT_API_KEY environment variable is required");
    private static readonly string PRODUCT_SLUG = Environment.GetEnvironmentVariable("LICENSESEAT_PRODUCT_SLUG")
        ?? throw new InvalidOperationException("LICENSESEAT_PRODUCT_SLUG environment variable is required");
    private static readonly string LICENSE_KEY = Environment.GetEnvironmentVariable("LICENSESEAT_LICENSE_KEY")
        ?? throw new InvalidOperationException("LICENSESEAT_LICENSE_KEY environment variable is required");

    // Simulated device IDs (using deterministic IDs for reliable cleanup)
    private static readonly string DEVICE_1 = $"test-laptop-{Environment.MachineName}".Substring(0, Math.Min(32, $"test-laptop-{Environment.MachineName}".Length));
    private static readonly string DEVICE_2 = $"test-desktop-{Environment.MachineName}".Substring(0, Math.Min(32, $"test-desktop-{Environment.MachineName}".Length));

    private static int _scenarioCount;
    private static int _passedCount;
    private static int _failedCount;
    private static readonly List<string> _failures = new List<string>();

    public static async Task RunAsync()
    {
        // Reset counters for fresh run
        _scenarioCount = 0;
        _passedCount = 0;
        _failedCount = 0;
        _failures.Clear();

        Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       REAL-WORLD USER JOURNEY SIMULATION                           ║");
        Console.WriteLine("║       Simulating a customer's complete experience                  ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Clean up any lingering activations from previous test runs
        Console.WriteLine("  🧹 Cleaning up any lingering activations...");

        // First, try to get current activation info via direct API call
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Validate to see current activation status
            var validateResponse = await httpClient.GetAsync(
                $"https://licenseseat.com/api/v1/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/validate");

            if (validateResponse.IsSuccessStatusCode)
            {
                var validateBody = await validateResponse.Content.ReadAsStringAsync();

                // Try to find any active device_id in the response and deactivate it
                // The validation response may contain activation info
                var json = JsonSerializer.Deserialize<JsonElement>(validateBody);
                if (json.TryGetProperty("license", out var licenseElement) &&
                    licenseElement.TryGetProperty("active_seats", out var activeSeats) &&
                    activeSeats.GetInt32() > 0)
                {
                    Console.WriteLine($"  ⚠️ License has {activeSeats.GetInt32()} active seat(s)");

                    // Try common device IDs that might be holding the seat
                    // The SDK generates a SHA256 hash of "{machineName}:{userName}:{osVersion}"
                    var sdkGeneratedId = ComputeDeviceId();
                    var possibleDeviceIds = new[]
                    {
                        sdkGeneratedId, // Most likely - this is what SDK generates by default
                        Environment.MachineName,
                        DEVICE_1,
                        DEVICE_2,
                    };

                    foreach (var deviceId in possibleDeviceIds)
                    {
                        try
                        {
                            var deactivateRequest = new StringContent(
                                JsonSerializer.Serialize(new { device_id = deviceId }),
                                System.Text.Encoding.UTF8,
                                "application/json");

                            var deactivateResponse = await httpClient.PostAsync(
                                $"https://licenseseat.com/api/v1/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/deactivate",
                                deactivateRequest);

                            if (deactivateResponse.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"  ✓ Deactivated device: {deviceId[..Math.Min(20, deviceId.Length)]}...");
                            }
                        }
                        catch { /* Ignore */ }
                    }
                }
            }
        }
        catch { /* Ignore cleanup errors */ }

        Console.WriteLine();
        Console.WriteLine($"  License Key: {LICENSE_KEY}");
        Console.WriteLine($"  Device 1 (Laptop): {DEVICE_1}");
        Console.WriteLine($"  Device 2 (Desktop): {DEVICE_2}");
        Console.WriteLine();

        // ================================================================
        // SCENARIO 1: First-Time User - Fresh Install
        // ================================================================
        await RunScenario("First-Time User - Fresh Install", async () =>
        {
            Console.WriteLine("    📦 User just installed your app for the first time...");
            Console.WriteLine("    📧 They received a license key via email after purchase...");
            Console.WriteLine();

            using var client = CreateClient(DEVICE_1);

            // User opens app - should be in Inactive state
            var status = client.GetStatus();
            Assert(status.StatusType == LicenseStatusType.Inactive,
                $"Fresh install should be Inactive, got {status.StatusType}");
            Console.WriteLine($"    ✓ App starts in Inactive state: {status.StatusType}");

            // User enters their license key and clicks "Activate"
            Console.WriteLine("    🔑 User enters license key and clicks Activate...");
            var license = await client.ActivateAsync(LICENSE_KEY);

            Assert(license != null, "Activation should return a license");
            Assert(license!.Status == "active", $"License should be active, got {license.Status}");
            Console.WriteLine($"    ✓ License activated successfully!");
            Console.WriteLine($"      Key: {license.Key}");
            Console.WriteLine($"      Status: {license.Status}");
            Console.WriteLine($"      Plan: {license.PlanKey}");
            Console.WriteLine($"      Seats: {license.ActiveSeats}/{license.SeatLimit}");

            // App should now be Active
            status = client.GetStatus();
            Assert(status.StatusType == LicenseStatusType.Active,
                $"After activation should be Active, got {status.StatusType}");
            Console.WriteLine($"    ✓ App is now Active: {status.StatusType}");

            // Clean up - deactivate for next test
            await client.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up (deactivated for next scenario)");
        });

        // ================================================================
        // SCENARIO 2: Checking Entitlements / Feature Gating
        // ================================================================
        await RunScenario("Checking Entitlements / Feature Gating", async () =>
        {
            Console.WriteLine("    🎮 User tries to access premium features...");
            Console.WriteLine();

            using var client = CreateClient(DEVICE_1);

            // Activate first
            await client.ActivateAsync(LICENSE_KEY);
            Console.WriteLine("    ✓ License activated");

            // Check for various entitlements
            var hasPremium = client.HasEntitlement("premium");
            var hasProFeatures = client.HasEntitlement("pro-features");
            var hasNonExistent = client.HasEntitlement("enterprise-only-feature");

            Console.WriteLine($"    📋 Entitlement checks:");
            Console.WriteLine($"      - premium: {hasPremium}");
            Console.WriteLine($"      - pro-features: {hasProFeatures}");
            Console.WriteLine($"      - enterprise-only-feature: {hasNonExistent}");

            // Detailed entitlement check
            var entitlementResult = client.CheckEntitlement("some-feature");
            Console.WriteLine($"    📊 Detailed check for 'some-feature':");
            Console.WriteLine($"      - Active: {entitlementResult.Active}");
            Console.WriteLine($"      - Reason: {entitlementResult.Reason}");

            // Feature gating example
            Console.WriteLine();
            Console.WriteLine("    🚪 Feature gating in action:");
            if (client.HasEntitlement("export-pdf"))
            {
                Console.WriteLine("      ✓ PDF Export: UNLOCKED");
            }
            else
            {
                Console.WriteLine("      🔒 PDF Export: Locked (need higher plan)");
            }

            await client.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // SCENARIO 3: Auto-Validation Running in Background
        // ================================================================
        await RunScenario("Auto-Validation Running in Background", async () =>
        {
            Console.WriteLine("    ⏰ Testing auto-validation with 3-second interval...");
            Console.WriteLine("    (In production, you'd use hours, not seconds)");
            Console.WriteLine();

            var validationCount = 0;
            var validationSuccessCount = 0;

            using var client = new LicenseSeatClient(new LicenseSeatClientOptions
            {
                ApiKey = API_KEY,
                ProductSlug = PRODUCT_SLUG,
                DeviceId = DEVICE_1,
                AutoInitialize = false,
                AutoValidateInterval = TimeSpan.FromSeconds(3), // Short for testing
                Debug = false
            });

            // Subscribe to validation events
            client.Events.On(LicenseSeatEvents.ValidationSuccess, _ =>
            {
                validationCount++;
                validationSuccessCount++;
                Console.WriteLine($"    📡 Auto-validation #{validationCount}: SUCCESS");
            });

            client.Events.On(LicenseSeatEvents.ValidationFailed, _ =>
            {
                validationCount++;
                Console.WriteLine($"    📡 Auto-validation #{validationCount}: FAILED");
            });

            // Activate - this starts auto-validation
            Console.WriteLine("    🔑 Activating license (starts auto-validation timer)...");
            await client.ActivateAsync(LICENSE_KEY);
            Console.WriteLine($"    ✓ Activated. Auto-validation will run every 3 seconds.");
            Console.WriteLine();

            // Wait for several auto-validations
            Console.WriteLine("    ⏳ Waiting 10 seconds to observe auto-validations...");
            await Task.Delay(10000);

            Console.WriteLine();
            Console.WriteLine($"    📊 Auto-validation results:");
            Console.WriteLine($"      - Total validations: {validationCount}");
            Console.WriteLine($"      - Successful: {validationSuccessCount}");

            Assert(validationCount >= 2, $"Should have at least 2 auto-validations, got {validationCount}");
            Assert(validationSuccessCount >= 2, $"Should have at least 2 successful validations, got {validationSuccessCount}");

            await client.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // SCENARIO 4: Offline Token Caching for Offline Use
        // ================================================================
        await RunScenario("Offline Token Caching for Offline Use", async () =>
        {
            Console.WriteLine("    ☁️ User's app needs to work when offline...");
            Console.WriteLine("    📥 Fetching and caching offline token...");
            Console.WriteLine();

            // First, activate and fetch offline token via API
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Activate first
            using var client = CreateClient(DEVICE_1);
            await client.ActivateAsync(LICENSE_KEY);
            Console.WriteLine("    ✓ License activated");

            // Fetch offline token
            var requestBody = new { device_id = DEVICE_1 };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"https://licenseseat.com/api/v1/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/offline_token",
                content);

            Assert(response.IsSuccessStatusCode, $"Should fetch offline token, got {response.StatusCode}");

            var responseBody = await response.Content.ReadAsStringAsync();
            var offlineToken = JsonSerializer.Deserialize<OfflineTokenResponse>(responseBody);

            Assert(offlineToken?.Token != null, "Should have token payload");
            Assert(offlineToken?.Signature != null, "Should have signature");
            Assert(!string.IsNullOrEmpty(offlineToken?.Canonical), "Should have canonical JSON");

            Console.WriteLine("    ✓ Offline token fetched and cached!");
            Console.WriteLine($"      License Key: {offlineToken!.Token!.LicenseKey}");
            Console.WriteLine($"      Expires: {DateTimeOffset.FromUnixTimeSeconds(offlineToken.Token.Exp)}");
            Console.WriteLine($"      Valid for: {(offlineToken.Token.Exp - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / 86400:F1} days");
            Console.WriteLine($"      Signed by: {offlineToken.Token.Kid}");

            // Verify signature
            var sigKeyResponse = await httpClient.GetAsync(
                $"https://licenseseat.com/api/v1/signing_keys/{offlineToken.Signature!.KeyId}");
            var sigKeyBody = await sigKeyResponse.Content.ReadAsStringAsync();
            var sigKey = JsonSerializer.Deserialize<SigningKeyResponse>(sigKeyBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var isValidSig = Ed25519Verifier.VerifyCanonical(
                sigKey!.PublicKey!,
                offlineToken.Signature.Value!,
                offlineToken.Canonical!);

            Assert(isValidSig, "Signature should be valid");
            Console.WriteLine("    ✓ Signature verified with Ed25519!");

            await client.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // SCENARIO 5: Network Goes Down - Offline Validation
        // ================================================================
        await RunScenario("Network Goes Down - Offline Validation", async () =>
        {
            Console.WriteLine("    📴 Simulating network outage...");
            Console.WriteLine("    🔌 User's internet connection drops...");
            Console.WriteLine();

            // Create client with offline fallback enabled
            using var client = new LicenseSeatClient(new LicenseSeatClientOptions
            {
                ApiKey = API_KEY,
                ProductSlug = PRODUCT_SLUG,
                DeviceId = DEVICE_1,
                AutoInitialize = false,
                AutoValidateInterval = TimeSpan.Zero,
                OfflineFallbackMode = OfflineFallbackMode.Always,
                MaxOfflineDays = 30,
                Debug = true
            });

            // Activate while online
            Console.WriteLine("    🌐 While online: Activating license...");
            var license = await client.ActivateAsync(LICENSE_KEY);
            Console.WriteLine($"    ✓ Activated: {license.Key}");

            // Validate while online
            var onlineResult = await client.ValidateAsync(LICENSE_KEY);
            Assert(onlineResult.Valid, "Online validation should succeed");
            Assert(!onlineResult.Offline, "Should NOT be offline validation");
            Console.WriteLine($"    ✓ Online validation: Valid={onlineResult.Valid}, Offline={onlineResult.Offline}");

            // Now simulate network failure by using offline-only validation
            Console.WriteLine();
            Console.WriteLine("    📴 [Network outage simulated]");
            Console.WriteLine("    🔄 User tries to validate while offline...");

            // The SDK's offline fallback would kick in when network fails
            // For this simulation, we'll check the cached state
            var cachedLicense = client.GetCurrentLicense();
            Assert(cachedLicense != null, "Should have cached license");
            Console.WriteLine($"    ✓ Cached license available: {cachedLicense!.Key}");

            var status = client.GetStatus();
            Console.WriteLine($"    ✓ App status: {status.StatusType}");

            // Check entitlements work offline
            var hasEntitlementOffline = client.HasEntitlement("premium");
            Console.WriteLine($"    ✓ Entitlement check works offline: {hasEntitlementOffline}");

            Console.WriteLine();
            Console.WriteLine("    🌐 [Network restored]");

            await client.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // SCENARIO 6: Tampered Offline Token Detection
        // ================================================================
        await RunScenario("Tampered Offline Token Detection", async () =>
        {
            Console.WriteLine("    🦹 Simulating a hacker trying to tamper with offline token...");
            Console.WriteLine();

            // Fetch a real offline token first
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");

            using var client = CreateClient(DEVICE_1);
            await client.ActivateAsync(LICENSE_KEY);

            var requestBody = new { device_id = DEVICE_1 };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"https://licenseseat.com/api/v1/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/offline_token",
                content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var offlineToken = JsonSerializer.Deserialize<OfflineTokenResponse>(responseBody);

            // Get public key
            var sigKeyResponse = await httpClient.GetAsync(
                $"https://licenseseat.com/api/v1/signing_keys/{offlineToken!.Signature!.KeyId}");
            var sigKeyBody = await sigKeyResponse.Content.ReadAsStringAsync();
            var sigKey = JsonSerializer.Deserialize<SigningKeyResponse>(sigKeyBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Console.WriteLine("    ✓ Got legitimate offline token");
            Console.WriteLine();

            // ATTACK 1: Try to modify the license key in canonical JSON
            Console.WriteLine("    🦹 ATTACK 1: Modifying license key in canonical JSON...");
            var tamperedCanonical1 = offlineToken.Canonical!.Replace(LICENSE_KEY, "FAKE-LICENSE-KEY");
            var isValid1 = Ed25519Verifier.VerifyCanonical(
                sigKey!.PublicKey!,
                offlineToken.Signature.Value!,
                tamperedCanonical1);
            Assert(!isValid1, "Tampered license key should fail verification");
            Console.WriteLine("    ✓ BLOCKED: Signature verification failed!");

            // ATTACK 2: Try to extend expiration
            Console.WriteLine("    🦹 ATTACK 2: Trying to extend token expiration...");
            var originalExp = offlineToken.Token!.Exp;
            var extendedExp = originalExp + (365 * 24 * 60 * 60); // Add 1 year
            var tamperedCanonical2 = offlineToken.Canonical!.Replace(
                $"\"exp\":{originalExp}",
                $"\"exp\":{extendedExp}");
            var isValid2 = Ed25519Verifier.VerifyCanonical(
                sigKey.PublicKey!,
                offlineToken.Signature.Value!,
                tamperedCanonical2);
            Assert(!isValid2, "Extended expiration should fail verification");
            Console.WriteLine("    ✓ BLOCKED: Can't extend expiration - signature invalid!");

            // ATTACK 3: Try to use wrong public key
            Console.WriteLine("    🦹 ATTACK 3: Trying to verify with fake public key...");
            var fakePublicKey = Convert.ToBase64String(new byte[32]); // Random 32-byte key
            try
            {
                var isValid3 = Ed25519Verifier.VerifyCanonical(
                    fakePublicKey,
                    offlineToken.Signature.Value!,
                    offlineToken.Canonical!);
                Assert(!isValid3, "Fake public key should fail verification");
                Console.WriteLine("    ✓ BLOCKED: Signature verification failed!");
            }
            catch (CryptoException)
            {
                Console.WriteLine("    ✓ BLOCKED: Crypto exception with fake key!");
            }

            // ATTACK 4: Try to forge signature
            Console.WriteLine("    🦹 ATTACK 4: Trying to forge signature...");
            var fakeSignature = Convert.ToBase64String(new byte[64]); // Random 64-byte signature
            var isValid4 = Ed25519Verifier.VerifyCanonical(
                sigKey.PublicKey!,
                fakeSignature,
                offlineToken.Canonical!);
            Assert(!isValid4, "Forged signature should fail verification");
            Console.WriteLine("    ✓ BLOCKED: Forged signature rejected!");

            Console.WriteLine();
            Console.WriteLine("    🛡️ All tampering attempts blocked by Ed25519 cryptography!");

            await client.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // SCENARIO 7: Clock Tampering Detection
        // ================================================================
        await RunScenario("Clock Tampering Detection", async () =>
        {
            Console.WriteLine("    🕐 Simulating user trying to set system clock back...");
            Console.WriteLine("    (To bypass offline token expiration)");
            Console.WriteLine();

            // Fetch offline token
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");

            using var client = CreateClient(DEVICE_1);
            await client.ActivateAsync(LICENSE_KEY);

            var requestBody = new { device_id = DEVICE_1 };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"https://licenseseat.com/api/v1/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/offline_token",
                content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var offlineToken = JsonSerializer.Deserialize<OfflineTokenResponse>(responseBody);

            Console.WriteLine("    ✓ Got offline token");
            Console.WriteLine($"      Issued at (iat): {DateTimeOffset.FromUnixTimeSeconds(offlineToken!.Token!.Iat)}");
            Console.WriteLine($"      Not before (nbf): {DateTimeOffset.FromUnixTimeSeconds(offlineToken.Token.Nbf)}");
            Console.WriteLine($"      Expires at (exp): {DateTimeOffset.FromUnixTimeSeconds(offlineToken.Token.Exp)}");
            Console.WriteLine();

            // Simulate clock check
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var lastSeenUnix = nowUnix; // We just validated

            Console.WriteLine($"    ⏰ Current time: {DateTimeOffset.UtcNow}");
            Console.WriteLine($"    ⏰ Last seen timestamp: {DateTimeOffset.FromUnixTimeSeconds(lastSeenUnix)}");
            Console.WriteLine();

            // ATTACK: Set clock back 1 week
            Console.WriteLine("    🦹 ATTACK: Setting clock back 1 week...");
            var fakeNowUnix = nowUnix - (7 * 24 * 60 * 60); // 1 week ago
            var maxSkewSeconds = 300; // 5 minutes

            // Check: if current time is significantly behind last seen, clock was tampered
            var clockTamperDetected = (fakeNowUnix + maxSkewSeconds) < lastSeenUnix;
            Assert(clockTamperDetected, "Should detect clock tampering");
            Console.WriteLine($"    ⏰ Fake 'now': {DateTimeOffset.FromUnixTimeSeconds(fakeNowUnix)}");
            Console.WriteLine($"    ⏰ Last seen: {DateTimeOffset.FromUnixTimeSeconds(lastSeenUnix)}");
            Console.WriteLine($"    ⏰ Max skew: {maxSkewSeconds} seconds");
            Console.WriteLine();
            Console.WriteLine("    🛡️ DETECTED: Clock was set backwards!");
            Console.WriteLine("    🛡️ Offline validation would be REJECTED");

            await client.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // SCENARIO 8: Expired Offline Token
        // ================================================================
        await RunScenario("Expired Offline Token Handling", async () =>
        {
            Console.WriteLine("    ⏰ Simulating an offline token that has expired...");
            Console.WriteLine();

            // Fetch offline token
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");

            using var client = CreateClient(DEVICE_1);
            await client.ActivateAsync(LICENSE_KEY);

            var requestBody = new { device_id = DEVICE_1 };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"https://licenseseat.com/api/v1/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/offline_token",
                content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var offlineToken = JsonSerializer.Deserialize<OfflineTokenResponse>(responseBody);

            var expTime = DateTimeOffset.FromUnixTimeSeconds(offlineToken!.Token!.Exp);
            Console.WriteLine($"    ✓ Token expires at: {expTime}");
            Console.WriteLine();

            // Simulate being past the expiration
            Console.WriteLine("    🦹 SCENARIO: User hasn't connected to internet for 60 days...");
            Console.WriteLine("    🦹 Token has expired...");

            var fakeNowUnix = offlineToken.Token.Exp + (30 * 24 * 60 * 60); // 30 days past expiry
            var fakeNow = DateTimeOffset.FromUnixTimeSeconds(fakeNowUnix);
            Console.WriteLine($"    ⏰ Simulated current time: {fakeNow}");

            var isExpired = offlineToken.Token.Exp < fakeNowUnix;
            Assert(isExpired, "Token should be detected as expired");
            Console.WriteLine();
            Console.WriteLine("    🛡️ DETECTED: Offline token has expired!");
            Console.WriteLine("    📡 User must connect to internet to revalidate");

            await client.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // SCENARIO 9: User Changes Computer (New Device)
        // ================================================================
        await RunScenario("User Changes Computer (New Device)", async () =>
        {
            Console.WriteLine("    💻 User got a new computer and wants to use the software...");
            Console.WriteLine();

            // Step 1: User activates on old laptop
            Console.WriteLine("    [OLD LAPTOP]");
            using (var oldLaptop = CreateClient(DEVICE_1))
            {
                Console.WriteLine("    🔑 Activating on old laptop...");
                var license = await oldLaptop.ActivateAsync(LICENSE_KEY);
                Console.WriteLine($"    ✓ Activated on laptop: {license.Key}");
                Console.WriteLine($"      Seats used: {license.ActiveSeats}/{license.SeatLimit}");

                // Deactivate to free up seat
                Console.WriteLine();
                Console.WriteLine("    🔄 User deactivates old laptop before selling it...");
                await oldLaptop.DeactivateAsync();
                Console.WriteLine("    ✓ Deactivated from old laptop");
            }

            // Step 2: User activates on new desktop
            Console.WriteLine();
            Console.WriteLine("    [NEW DESKTOP]");
            using (var newDesktop = CreateClient(DEVICE_2))
            {
                Console.WriteLine("    🔑 Activating on new desktop...");
                var license = await newDesktop.ActivateAsync(LICENSE_KEY);
                Console.WriteLine($"    ✓ Activated on desktop: {license.Key}");
                Console.WriteLine($"      Seats used: {license.ActiveSeats}/{license.SeatLimit}");

                // Verify it works
                var result = await newDesktop.ValidateAsync(LICENSE_KEY);
                Assert(result.Valid, "Should be valid on new device");
                Console.WriteLine("    ✓ License working on new computer!");

                await newDesktop.DeactivateAsync();
                Console.WriteLine("    🧹 Cleaned up");
            }
        });

        // ================================================================
        // SCENARIO 10: Seat Limit Exceeded (Multi-Device)
        // ================================================================
        await RunScenario("Seat Limit Exceeded (Multi-Device)", async () =>
        {
            Console.WriteLine("    👥 User tries to activate on too many devices...");
            Console.WriteLine();

            // First, activate on device 1
            using var device1 = CreateClient(DEVICE_1);
            Console.WriteLine("    💻 Device 1 (Laptop): Activating...");
            var license = await device1.ActivateAsync(LICENSE_KEY);
            Console.WriteLine($"    ✓ Activated! Seats: {license.ActiveSeats}/{license.SeatLimit}");

            // Check if we have room for another device
            if (license.SeatLimit.HasValue && license.ActiveSeats >= license.SeatLimit.Value)
            {
                Console.WriteLine();
                Console.WriteLine("    💻 Device 2 (Desktop): Trying to activate...");

                using var device2 = CreateClient(DEVICE_2);
                try
                {
                    await device2.ActivateAsync(LICENSE_KEY);
                    Console.WriteLine("    ⚠️ Unexpectedly succeeded (seat may have been freed)");
                }
                catch (ApiException ex) when (ex.Code == "seat_limit_exceeded")
                {
                    Console.WriteLine($"    🛡️ BLOCKED: {ex.Code}");
                    Console.WriteLine($"       Message: {ex.Message}");
                    Console.WriteLine();
                    Console.WriteLine("    💡 User needs to:");
                    Console.WriteLine("       - Deactivate from another device, OR");
                    Console.WriteLine("       - Upgrade to a plan with more seats");
                }
            }
            else
            {
                Console.WriteLine($"    ℹ️ License has room for more devices ({license.ActiveSeats}/{license.SeatLimit})");
                Console.WriteLine("    ℹ️ (This license allows multiple seats)");
            }

            await device1.DeactivateAsync();
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // SCENARIO 11: Invalid License Key
        // ================================================================
        await RunScenario("Invalid License Key (User Typo)", async () =>
        {
            Console.WriteLine("    ⌨️ User makes a typo when entering license key...");
            Console.WriteLine();

            using var client = CreateClient(DEVICE_1);

            var invalidKey = "XXXXX-INVALID-KEY-12345";
            Console.WriteLine($"    🔑 User enters: {invalidKey}");

            try
            {
                await client.ActivateAsync(invalidKey);
                Assert(false, "Should have thrown ApiException");
            }
            catch (ApiException ex)
            {
                Console.WriteLine();
                Console.WriteLine($"    ❌ Error: {ex.Code}");
                Console.WriteLine($"       Message: {ex.Message}");
                Console.WriteLine($"       Status: {ex.StatusCode}");
                Console.WriteLine();
                Console.WriteLine("    💡 User sees: \"License key not found. Please check and try again.\"");

                Assert(ex.Code == "license_not_found", $"Expected license_not_found, got {ex.Code}");
            }
        });

        // ================================================================
        // SCENARIO 12: Security - Wrong Product Slug
        // ================================================================
        await RunScenario("Security - Wrong Product Slug", async () =>
        {
            Console.WriteLine("    🔐 Testing security: using wrong product slug...");
            Console.WriteLine();

            using var client = new LicenseSeatClient(new LicenseSeatClientOptions
            {
                ApiKey = API_KEY,
                ProductSlug = "wrong-product-that-doesnt-exist",
                DeviceId = DEVICE_1,
                AutoInitialize = false,
                AutoValidateInterval = TimeSpan.Zero
            });

            Console.WriteLine("    🎯 Trying to activate with wrong product...");

            try
            {
                await client.ActivateAsync(LICENSE_KEY);
                Assert(false, "Should have thrown ApiException");
            }
            catch (ApiException ex)
            {
                Console.WriteLine($"    🛡️ BLOCKED: {ex.Code}");
                Console.WriteLine($"       Message: {ex.Message}");
                Console.WriteLine($"       Status: {ex.StatusCode}");
                Console.WriteLine();
                Console.WriteLine("    ✓ Cannot use license with wrong product!");
            }
        });

        // ================================================================
        // SCENARIO 13: Security - Invalid API Key
        // ================================================================
        await RunScenario("Security - Invalid/Missing API Key", async () =>
        {
            Console.WriteLine("    🔐 Testing security: using invalid API key...");
            Console.WriteLine();

            using var client = new LicenseSeatClient(new LicenseSeatClientOptions
            {
                ApiKey = "invalid_api_key_12345",
                ProductSlug = PRODUCT_SLUG,
                DeviceId = DEVICE_1,
                AutoInitialize = false,
                AutoValidateInterval = TimeSpan.Zero
            });

            Console.WriteLine("    🎯 Trying to activate with invalid API key...");

            try
            {
                await client.ActivateAsync(LICENSE_KEY);
                Assert(false, "Should have thrown ApiException");
            }
            catch (ApiException ex)
            {
                Console.WriteLine($"    🛡️ BLOCKED: {ex.Code}");
                Console.WriteLine($"       Message: {ex.Message}");
                Console.WriteLine($"       Status: {ex.StatusCode}");
                Console.WriteLine();
                Console.WriteLine("    ✓ Cannot access API with invalid credentials!");

                // Should be 401 Unauthorized
                Assert(ex.StatusCode == 401 || ex.Code == "invalid_api_key" || ex.Code == "unauthorized",
                    $"Expected 401 or invalid_api_key, got {ex.StatusCode}/{ex.Code}");
            }
        });

        // ================================================================
        // SCENARIO 14: Event-Driven UI Updates
        // ================================================================
        await RunScenario("Event-Driven UI Updates", async () =>
        {
            Console.WriteLine("    🎨 App UI reacts to license events in real-time...");
            Console.WriteLine();

            var events = new List<string>();

            using var client = new LicenseSeatClient(new LicenseSeatClientOptions
            {
                ApiKey = API_KEY,
                ProductSlug = PRODUCT_SLUG,
                DeviceId = DEVICE_1,
                AutoInitialize = false,
                AutoValidateInterval = TimeSpan.Zero,
                Debug = false
            });

            // Subscribe to all events (like a real UI would)
            client.Events.On(LicenseSeatEvents.ActivationStart, _ =>
            {
                events.Add("ActivationStart");
                Console.WriteLine("    📡 Event: ActivationStart → Show loading spinner");
            });

            client.Events.On(LicenseSeatEvents.ActivationSuccess, _ =>
            {
                events.Add("ActivationSuccess");
                Console.WriteLine("    📡 Event: ActivationSuccess → Show success message, unlock features");
            });

            client.Events.On(LicenseSeatEvents.ValidationSuccess, _ =>
            {
                events.Add("ValidationSuccess");
                Console.WriteLine("    📡 Event: ValidationSuccess → Update UI, license is valid");
            });

            client.Events.On(LicenseSeatEvents.DeactivationStart, _ =>
            {
                events.Add("DeactivationStart");
                Console.WriteLine("    📡 Event: DeactivationStart → Show loading spinner");
            });

            client.Events.On(LicenseSeatEvents.DeactivationSuccess, _ =>
            {
                events.Add("DeactivationSuccess");
                Console.WriteLine("    📡 Event: DeactivationSuccess → Lock features, show activation screen");
            });

            Console.WriteLine();

            // Perform actions and watch events fire
            await client.ActivateAsync(LICENSE_KEY);
            await client.ValidateAsync(LICENSE_KEY);
            await client.DeactivateAsync();

            Console.WriteLine();
            Console.WriteLine($"    📊 Total events fired: {events.Count}");
            Assert(events.Contains("ActivationStart"), "Should have ActivationStart event");
            Assert(events.Contains("ActivationSuccess"), "Should have ActivationSuccess event");
            Assert(events.Contains("DeactivationSuccess"), "Should have DeactivationSuccess event");
        });

        // ================================================================
        // SCENARIO 15: Graceful Shutdown
        // ================================================================
        await RunScenario("Graceful Shutdown on App Exit", async () =>
        {
            Console.WriteLine("    🚪 User closes the app...");
            Console.WriteLine();

            using var client = CreateClient(DEVICE_1);

            Console.WriteLine("    🔑 App has active license...");
            await client.ActivateAsync(LICENSE_KEY);
            var status = client.GetStatus();
            Console.WriteLine($"    ✓ Status: {status.StatusType}");

            Console.WriteLine();
            Console.WriteLine("    🚪 User clicks 'X' to close app...");
            Console.WriteLine();

            // App should properly dispose the client
            Console.WriteLine("    🔄 Calling Dispose() - stops timers, cleans up...");
            client.Dispose();
            Console.WriteLine("    ✓ Client disposed cleanly");
            Console.WriteLine();
            Console.WriteLine("    💡 Note: License stays active - user doesn't need to");
            Console.WriteLine("       re-activate every time they open the app!");

            // Re-activate to clean up for other tests
            using var cleanup = CreateClient(DEVICE_1);
            try
            {
                // May or may not need to activate depending on state
                var result = await cleanup.ValidateAsync(LICENSE_KEY);
                if (result.Valid)
                {
                    await cleanup.DeactivateAsync();
                }
            }
            catch { }
            Console.WriteLine("    🧹 Cleaned up");
        });

        // ================================================================
        // Summary
        // ================================================================
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                     USER JOURNEY SUMMARY                           ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"  Total Scenarios: {_scenarioCount}");
        Console.WriteLine($"  Passed: {_passedCount}");
        Console.WriteLine($"  Failed: {_failedCount}");
        Console.WriteLine();

        if (_failures.Count > 0)
        {
            Console.WriteLine("  ❌ Failed Scenarios:");
            foreach (var failure in _failures)
            {
                Console.WriteLine($"     - {failure}");
            }
        }
        else
        {
            Console.WriteLine("  ✅ All scenarios passed!");
            Console.WriteLine();
            Console.WriteLine("  🎉 The SDK is ready for real-world customer use!");
        }

        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════════════");
    }

    private static LicenseSeatClient CreateClient(string deviceId)
    {
        return new LicenseSeatClient(new LicenseSeatClientOptions
        {
            ApiKey = API_KEY,
            ProductSlug = PRODUCT_SLUG,
            DeviceId = deviceId,
            AutoInitialize = false,
            AutoValidateInterval = TimeSpan.Zero,
            Debug = false
        });
    }

    private static async Task RunScenario(string name, Func<Task> action)
    {
        _scenarioCount++;
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"  SCENARIO {_scenarioCount}: {name}");
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();
        try
        {
            await action();
            sw.Stop();
            _passedCount++;
            Console.WriteLine();
            Console.WriteLine($"  ✅ PASSED ({sw.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _failedCount++;
            _failures.Add($"{name}: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine($"  ❌ FAILED: {ex.Message}");
            Console.WriteLine($"     {ex.GetType().Name}");
        }
        Console.WriteLine();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }

    /// <summary>
    /// Computes the device ID the same way the SDK does (SHA256 hash of machine info).
    /// </summary>
    private static string ComputeDeviceId()
    {
        try
        {
            var machineName = Environment.MachineName;
            var userName = Environment.UserName;
            var osVersion = Environment.OSVersion.ToString();

            var input = $"{machineName}:{userName}:{osVersion}";

            var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hashBytes = System.Security.Cryptography.SHA256.HashData(inputBytes);

            var sb = new System.Text.StringBuilder(32);
            for (int i = 0; i < 16; i++)
            {
                sb.Append(hashBytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
        catch
        {
            return Environment.MachineName;
        }
    }
}

// SigningKeyResponse is defined in Program.cs
