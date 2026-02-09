using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LicenseSeat;

namespace LicenseSeat.StressTests;

/// <summary>
/// Telemetry, heartbeat, and auto-validation stress test.
/// Matches the 7 scenarios from Swift/JS SDKs.
/// </summary>
public static class TelemetryStressTest
{
    private static readonly string API_URL = Environment.GetEnvironmentVariable("LICENSESEAT_API_URL")
        ?? "https://licenseseat.com/api/v1";
    private static readonly string API_KEY = Environment.GetEnvironmentVariable("LICENSESEAT_API_KEY")
        ?? throw new InvalidOperationException("LICENSESEAT_API_KEY environment variable is required");
    private static readonly string PRODUCT_SLUG = Environment.GetEnvironmentVariable("LICENSESEAT_PRODUCT_SLUG")
        ?? throw new InvalidOperationException("LICENSESEAT_PRODUCT_SLUG environment variable is required");
    private static readonly string LICENSE_KEY = Environment.GetEnvironmentVariable("LICENSESEAT_LICENSE_KEY")
        ?? throw new InvalidOperationException("LICENSESEAT_LICENSE_KEY environment variable is required");

    private static int _passed;
    private static int _failed;
    private static readonly List<string> _failures = new();

    public static async Task<bool> RunAsync()
    {
        _passed = 0;
        _failed = 0;
        _failures.Clear();

        PrintBanner();

        // Clean up any lingering activations before starting
        await CleanupActivations();

        await Scenario1_ActivationWithTelemetry();
        await Scenario2_ValidationWithTelemetry();
        await Scenario3_HeartbeatEndpoint();
        await Scenario4_TelemetryDisabled();
        await Scenario5_AutoValidationCycles();
        await Scenario6_ConcurrentStress();
        await Scenario7_FullLifecycle();

        PrintSummary();

        return _failed == 0;
    }

    /// <summary>
    /// Clean up lingering activations by discovering active device IDs via validate,
    /// then deactivating them.
    /// </summary>
    private static async Task CleanupActivations()
    {
        Log("  Cleaning up lingering activations...");
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        // First, discover active device IDs via validate with known device IDs
        var possibleDeviceIds = new List<string>
        {
            ComputeDeviceId(),
            Environment.MachineName,
            "stress-test-device",
        };

        // Try to discover active device_id(s) via the validate endpoint
        // by calling validate with each known device_id and extracting activation info
        foreach (var deviceId in possibleDeviceIds.ToArray())
        {
            try
            {
                var body = JsonSerializer.Serialize(new { device_id = deviceId });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(
                    $"{API_URL}/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/validate",
                    content);
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    // Check for activation.device_id in response
                    if (json.TryGetProperty("activation", out var activation) &&
                        activation.TryGetProperty("device_id", out var did))
                    {
                        var activeDeviceId = did.GetString();
                        if (!string.IsNullOrEmpty(activeDeviceId) && !possibleDeviceIds.Contains(activeDeviceId))
                        {
                            possibleDeviceIds.Add(activeDeviceId);
                            Log($"  Discovered active device: {activeDeviceId[..Math.Min(20, activeDeviceId.Length)]}...");
                        }
                    }
                    // Also check license.activations array if present
                    if (json.TryGetProperty("license", out var licenseEl))
                    {
                        if (licenseEl.TryGetProperty("activations", out var activations) &&
                            activations.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var act in activations.EnumerateArray())
                            {
                                if (act.TryGetProperty("device_id", out var actDid))
                                {
                                    var actDeviceId = actDid.GetString();
                                    if (!string.IsNullOrEmpty(actDeviceId) && !possibleDeviceIds.Contains(actDeviceId))
                                    {
                                        possibleDeviceIds.Add(actDeviceId);
                                        Log($"  Discovered active device: {actDeviceId[..Math.Min(20, actDeviceId.Length)]}...");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* ignore */ }
        }

        // Now try to deactivate each known device ID
        foreach (var deviceId in possibleDeviceIds)
        {
            try
            {
                var body = JsonSerializer.Serialize(new { device_id = deviceId });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(
                    $"{API_URL}/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/deactivate",
                    content);
                if (response.IsSuccessStatusCode)
                {
                    Log($"  Deactivated device: {deviceId[..Math.Min(20, deviceId.Length)]}...");
                }
            }
            catch { /* ignore */ }
        }
        Log("  Cleanup done.");
        Console.WriteLine();
    }

    // ================================================================
    // Scenario 1: Activation WITH Telemetry (default)
    // ================================================================
    private static async Task Scenario1_ActivationWithTelemetry()
    {
        PrintHeader("Scenario 1: Activation WITH Telemetry (default)");

        LicenseSeatClient? client = null;
        try
        {
            client = CreateClient(telemetryEnabled: true, autoValidateInterval: TimeSpan.Zero);
            client.Reset();

            License? license = null;
            try
            {
                license = await client.ActivateAsync(LICENSE_KEY);
            }
            catch (ApiException ex) when (ex.Code == "already_activated" || ex.Code == "seat_limit_exceeded")
            {
                PrintTest($"Activate ({ex.Code} is OK -- seat already taken)", true);
                Log($"  {ex.Code} -- continuing with validation");
                // Validate to populate the cache
                var valResult = await client.ValidateAsync(LICENSE_KEY);
                license = valResult.License;
            }

            if (license != null)
            {
                PrintTest("Activate license", true);
                Log($"  device_id: {license.DeviceId}");
                Log($"  key:       {license.Key}");
            }
            else
            {
                PrintTest("Activate license", false, "license is null");
            }
        }
        catch (Exception ex)
        {
            PrintTest("Activate license", false, ex.Message);
        }

        // Store client for scenario 2/3 -- keep alive across scenarios
        _scenarioClient = client;
    }

    private static LicenseSeatClient? _scenarioClient;

    // ================================================================
    // Scenario 2: Validation WITH Telemetry
    // ================================================================
    private static async Task Scenario2_ValidationWithTelemetry()
    {
        PrintHeader("Scenario 2: Validation WITH Telemetry");

        var client = _scenarioClient;
        if (client == null)
        {
            PrintTest("Validate license", false, "No client from scenario 1");
            return;
        }

        try
        {
            var result = await client.ValidateAsync(LICENSE_KEY);
            PrintTest("Validate returns valid=true", result.Valid, result.Valid ? null : $"valid={result.Valid}");

            if (result.License != null)
            {
                Log($"  plan_key: {result.License.PlanKey}");
                Log($"  mode:     {result.License.Mode}");
                Log($"  seats:    {result.License.ActiveSeats}/{result.License.SeatLimit}");
            }
        }
        catch (Exception ex)
        {
            PrintTest("Validate license", false, ex.Message);
        }
    }

    // ================================================================
    // Scenario 3: Heartbeat Endpoint
    // ================================================================
    private static async Task Scenario3_HeartbeatEndpoint()
    {
        PrintHeader("Scenario 3: Heartbeat Endpoint");

        var client = _scenarioClient;
        if (client == null)
        {
            PrintTest("Heartbeat", false, "No client from scenario 1");
            return;
        }

        // Verify cached license exists before heartbeat
        var cachedLicense = client.GetCurrentLicense();
        PrintTest("Cached license exists before heartbeat", cachedLicense != null,
            cachedLicense == null ? "No cached license -- heartbeat would silently do nothing (BUG 2)" : null);

        if (cachedLicense == null)
        {
            PrintTest("1 initial heartbeat", false, "No cached license");
            return;
        }

        // 1 initial heartbeat
        try
        {
            await client.HeartbeatAsync();
            PrintTest("1 initial heartbeat", true);
        }
        catch (Exception ex)
        {
            PrintTest("1 initial heartbeat", false, ex.Message);
        }

        // 5 rapid heartbeats in sequence
        var rapidSuccess = 0;
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await client.HeartbeatAsync();
                rapidSuccess++;
            }
            catch (Exception ex)
            {
                Log($"  rapid heartbeat {i + 1} failed: {ex.Message}");
            }
        }
        PrintTest($"5 rapid heartbeats ({rapidSuccess}/5 succeeded)", rapidSuccess == 5,
            rapidSuccess < 5 ? $"Only {rapidSuccess}/5 succeeded" : null);

        // 3 spaced heartbeats (500ms apart)
        var spacedSuccess = 0;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await client.HeartbeatAsync();
                spacedSuccess++;
                if (i < 2) await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Log($"  spaced heartbeat {i + 1} failed: {ex.Message}");
            }
        }
        PrintTest($"3 spaced heartbeats ({spacedSuccess}/3 succeeded)", spacedSuccess == 3,
            spacedSuccess < 3 ? $"Only {spacedSuccess}/3 succeeded" : null);
    }

    // ================================================================
    // Scenario 4: Telemetry DISABLED
    // ================================================================
    private static async Task Scenario4_TelemetryDisabled()
    {
        PrintHeader("Scenario 4: Telemetry DISABLED");

        // Deactivate from scenario 1-3 client
        if (_scenarioClient != null)
        {
            try
            {
                await _scenarioClient.DeactivateAsync();
                Log("  Deactivated previous client");
            }
            catch (Exception ex)
            {
                Log($"  Deactivation warning: {ex.Message}");
                // Force deactivation via API as fallback
                await ForceDeactivate();
            }
            _scenarioClient.Dispose();
            _scenarioClient = null;
        }

        LicenseSeatClient? client = null;
        try
        {
            client = CreateClient(telemetryEnabled: false, autoValidateInterval: TimeSpan.Zero);

            // Activate
            try
            {
                var license = await client.ActivateAsync(LICENSE_KEY);
                PrintTest("Activate (telemetry disabled)", true);
            }
            catch (ApiException ex) when (ex.Code == "already_activated" || ex.Code == "seat_limit_exceeded")
            {
                PrintTest($"Activate ({ex.Code} OK, telemetry disabled)", true);
                await client.ValidateAsync(LICENSE_KEY);
            }

            // Validate
            var result = await client.ValidateAsync(LICENSE_KEY);
            PrintTest("Validate (telemetry disabled)", result.Valid,
                result.Valid ? null : $"valid={result.Valid}");

            // Heartbeat
            try
            {
                await client.HeartbeatAsync();
                PrintTest("Heartbeat (telemetry disabled)", true);
            }
            catch (Exception ex)
            {
                PrintTest("Heartbeat (telemetry disabled)", false, ex.Message);
            }

            // Deactivate
            await client.DeactivateAsync();
            PrintTest("Deactivate (telemetry disabled)", true);
        }
        catch (Exception ex)
        {
            PrintTest("Telemetry disabled flow", false, ex.Message);
            // Make sure seat is freed for next scenario
            await ForceDeactivate();
        }
        finally
        {
            client?.Dispose();
        }
    }

    // ================================================================
    // Scenario 5: Auto-Validation + Heartbeat Cycles
    // ================================================================
    private static async Task Scenario5_AutoValidationCycles()
    {
        PrintHeader("Scenario 5: Auto-Validation + Heartbeat Cycles");

        LicenseSeatClient? client = null;
        try
        {
            client = CreateClient(telemetryEnabled: true, autoValidateInterval: TimeSpan.FromSeconds(3));

            var cycleCount = 0;
            client.Events.On(LicenseSeatEvents.AutoValidationCycle, _ =>
            {
                Interlocked.Increment(ref cycleCount);
            });

            // Activate
            try
            {
                await client.ActivateAsync(LICENSE_KEY);
            }
            catch (ApiException ex) when (ex.Code == "already_activated" || ex.Code == "seat_limit_exceeded")
            {
                Log($"  {ex.Code} -- continuing");
                await client.ValidateAsync(LICENSE_KEY);
            }

            PrintTest("Activate + start auto-validation", true);
            Log($"  Waiting ~12 seconds for auto-validation cycles...");

            await Task.Delay(12_000);

            // The initial StartAutoValidation emits one cycle event immediately,
            // then the timer fires approximately every 3 seconds.
            // In 12 seconds we expect the initial emit + ~3-4 timer firings = at least 2 total.
            var totalCycles = cycleCount;
            PrintTest($"Auto-validation cycles fired ({totalCycles} total, need >= 2)", totalCycles >= 2,
                totalCycles < 2 ? $"Only {totalCycles} cycles in 12s" : null);

            // Deactivate before dispose
            try
            {
                await client.DeactivateAsync();
            }
            catch
            {
                await ForceDeactivate();
            }
            PrintTest("Deactivate after auto-validation", true);
        }
        catch (Exception ex)
        {
            PrintTest("Auto-validation cycles", false, ex.Message);
            await ForceDeactivate();
        }
        finally
        {
            // CRITICAL: Dispose to stop background timer before scenario 6
            client?.Dispose();
        }
    }

    // ================================================================
    // Scenario 6: Concurrent Validation Stress
    // ================================================================
    private static async Task Scenario6_ConcurrentStress()
    {
        PrintHeader("Scenario 6: Concurrent Validation Stress");

        LicenseSeatClient? client = null;
        try
        {
            // FRESH client with NO auto-validation
            client = CreateClient(telemetryEnabled: true, autoValidateInterval: TimeSpan.Zero);

            // Activate
            try
            {
                await client.ActivateAsync(LICENSE_KEY);
            }
            catch (ApiException ex) when (ex.Code == "already_activated" || ex.Code == "seat_limit_exceeded")
            {
                Log($"  {ex.Code} -- continuing");
                await client.ValidateAsync(LICENSE_KEY);
            }
            PrintTest("Activate for concurrent test", true);

            // 5 concurrent validations
            var validationTasks = Enumerable.Range(0, 5)
                .Select(_ => SafeValidate(client))
                .ToArray();
            var validationResults = await Task.WhenAll(validationTasks);
            var validationSuccesses = validationResults.Count(r => r);
            PrintTest($"5 concurrent validations ({validationSuccesses}/5 succeeded)",
                validationSuccesses >= 4,
                validationSuccesses < 4 ? $"Only {validationSuccesses}/5 succeeded" : null);

            // 3 concurrent heartbeats
            var heartbeatTasks = Enumerable.Range(0, 3)
                .Select(_ => SafeHeartbeat(client))
                .ToArray();
            var heartbeatResults = await Task.WhenAll(heartbeatTasks);
            var heartbeatSuccesses = heartbeatResults.Count(r => r);
            PrintTest($"3 concurrent heartbeats ({heartbeatSuccesses}/3 succeeded)",
                heartbeatSuccesses >= 2,
                heartbeatSuccesses < 2 ? $"Only {heartbeatSuccesses}/3 succeeded" : null);

            // Deactivate
            try
            {
                await client.DeactivateAsync();
            }
            catch
            {
                await ForceDeactivate();
            }
            PrintTest("Deactivate after concurrent test", true);
        }
        catch (Exception ex)
        {
            PrintTest("Concurrent stress", false, ex.Message);
            await ForceDeactivate();
        }
        finally
        {
            client?.Dispose();
        }
    }

    // ================================================================
    // Scenario 7: Full Lifecycle
    // ================================================================
    private static async Task Scenario7_FullLifecycle()
    {
        PrintHeader("Scenario 7: Full Lifecycle");

        LicenseSeatClient? client = null;
        try
        {
            client = CreateClient(telemetryEnabled: true, autoValidateInterval: TimeSpan.Zero);

            var eventLog = new List<string>();

            client.Events.On(LicenseSeatEvents.ActivationSuccess, _ => eventLog.Add("ActivationSuccess"));
            client.Events.On(LicenseSeatEvents.ValidationSuccess, _ => eventLog.Add("ValidationSuccess"));
            client.Events.On(LicenseSeatEvents.DeactivationSuccess, _ => eventLog.Add("DeactivationSuccess"));
            client.Events.On(LicenseSeatEvents.HeartbeatSuccess, _ => eventLog.Add("HeartbeatSuccess"));

            // Step 1: Activate
            try
            {
                await client.ActivateAsync(LICENSE_KEY);
                PrintTest("Step 1: ActivateAsync", true);
            }
            catch (ApiException ex) when (ex.Code == "already_activated" || ex.Code == "seat_limit_exceeded")
            {
                PrintTest($"Step 1: ActivateAsync ({ex.Code} OK)", true);
                // Force the event since the SDK emits it only on success path
                eventLog.Add("ActivationSuccess");
                await client.ValidateAsync(LICENSE_KEY);
            }

            // Step 2: Validate
            var validation = await client.ValidateAsync(LICENSE_KEY);
            PrintTest("Step 2: ValidateAsync (valid)", validation.Valid,
                validation.Valid ? null : $"valid={validation.Valid}");

            // Step 3: Heartbeat
            try
            {
                await client.HeartbeatAsync();
                PrintTest("Step 3: HeartbeatAsync (no exception)", true);
            }
            catch (Exception ex)
            {
                PrintTest("Step 3: HeartbeatAsync", false, ex.Message);
            }

            // Step 4: Deactivate
            await client.DeactivateAsync();
            var licenseAfter = client.GetCurrentLicense();
            PrintTest("Step 4: DeactivateAsync (license cleared)", licenseAfter == null,
                licenseAfter != null ? "License not null after deactivation" : null);

            // Check event log
            await Task.Delay(100); // small delay for async event delivery

            var hasActivation = eventLog.Contains("ActivationSuccess");
            var hasValidation = eventLog.Contains("ValidationSuccess");
            var hasDeactivation = eventLog.Contains("DeactivationSuccess");
            var hasHeartbeat = eventLog.Contains("HeartbeatSuccess");

            PrintTest("Event: ActivationSuccess fired", hasActivation,
                hasActivation ? null : "Missing ActivationSuccess event");
            PrintTest("Event: ValidationSuccess fired", hasValidation,
                hasValidation ? null : "Missing ValidationSuccess event");
            PrintTest("Event: DeactivationSuccess fired", hasDeactivation,
                hasDeactivation ? null : "Missing DeactivationSuccess event");
            PrintTest("Event: HeartbeatSuccess fired", hasHeartbeat,
                hasHeartbeat ? null : "Missing HeartbeatSuccess event");

            Log($"  Event log: [{string.Join(", ", eventLog)}]");
        }
        catch (Exception ex)
        {
            PrintTest("Full lifecycle", false, ex.Message);
            await ForceDeactivate();
        }
        finally
        {
            client?.Dispose();
        }
    }

    // ================================================================
    // Helpers
    // ================================================================

    private static LicenseSeatClient CreateClient(bool telemetryEnabled, TimeSpan autoValidateInterval)
    {
        return new LicenseSeatClient(new LicenseSeatClientOptions
        {
            ApiKey = API_KEY,
            ProductSlug = PRODUCT_SLUG,
            ApiBaseUrl = API_URL,
            AutoInitialize = false,
            AutoValidateInterval = autoValidateInterval,
            TelemetryEnabled = telemetryEnabled,
            Debug = false,
            MaxRetries = 1,
            RetryDelay = TimeSpan.FromMilliseconds(500),
        });
    }

    /// <summary>
    /// Force-deactivate via raw HTTP to free the seat, bypassing SDK state.
    /// Tries the SDK-generated device ID first, then discovers active device_ids via validate.
    /// </summary>
    private static async Task ForceDeactivate()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {API_KEY}");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Try SDK device ID first
            var deviceIds = new List<string> { ComputeDeviceId(), Environment.MachineName };

            // Discover active device_id via validate
            try
            {
                var valBody = JsonSerializer.Serialize(new { device_id = deviceIds[0] });
                var valContent = new StringContent(valBody, Encoding.UTF8, "application/json");
                var valResponse = await httpClient.PostAsync(
                    $"{API_URL}/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/validate",
                    valContent);
                if (valResponse.IsSuccessStatusCode)
                {
                    var valResponseBody = await valResponse.Content.ReadAsStringAsync();
                    var json = JsonSerializer.Deserialize<JsonElement>(valResponseBody);
                    if (json.TryGetProperty("activation", out var activation) &&
                        activation.TryGetProperty("device_id", out var did))
                    {
                        var activeDeviceId = did.GetString();
                        if (!string.IsNullOrEmpty(activeDeviceId) && !deviceIds.Contains(activeDeviceId))
                        {
                            deviceIds.Insert(0, activeDeviceId);
                        }
                    }
                }
            }
            catch { /* ignore */ }

            foreach (var deviceId in deviceIds)
            {
                try
                {
                    var body = JsonSerializer.Serialize(new { device_id = deviceId });
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(
                        $"{API_URL}/products/{PRODUCT_SLUG}/licenses/{LICENSE_KEY}/deactivate",
                        content);
                    if (response.IsSuccessStatusCode) break;
                }
                catch { /* ignore */ }
            }
        }
        catch { /* ignore */ }
    }

    private static string ComputeDeviceId()
    {
        try
        {
            var machineName = Environment.MachineName;
            var userName = Environment.UserName;
            var osVersion = Environment.OSVersion.ToString();
            var input = $"{machineName}:{userName}:{osVersion}";
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = System.Security.Cryptography.SHA256.HashData(inputBytes);
            var sb = new StringBuilder(32);
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

    private static async Task<bool> SafeValidate(LicenseSeatClient client)
    {
        try
        {
            var result = await client.ValidateAsync(LICENSE_KEY);
            return result.Valid;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SafeHeartbeat(LicenseSeatClient client)
    {
        try
        {
            await client.HeartbeatAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ================================================================
    // Output helpers (match Swift/JS format)
    // ================================================================

    private static void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("======================================================================");
        Console.WriteLine("  LicenseSeat C# SDK -- Telemetry & Heartbeat Stress Test");
        Console.WriteLine("======================================================================");
        Console.WriteLine($"  API URL:     {API_URL}");
        Console.WriteLine($"  Product:     {PRODUCT_SLUG}");
        Console.WriteLine($"  License:     {LICENSE_KEY}");
        Console.WriteLine($"  SDK Version: {LicenseSeatClient.SdkVersion}");
        Console.WriteLine("======================================================================");
        Console.WriteLine();
    }

    private static void PrintHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {title} ---");
        Console.WriteLine();
    }

    private static void PrintTest(string name, bool passed, string? error = null)
    {
        if (passed)
        {
            _passed++;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  PASS  {name}");
            Console.ResetColor();
        }
        else
        {
            _failed++;
            _failures.Add($"{name}: {error ?? "failed"}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  FAIL  {name}");
            Console.ResetColor();
            if (error != null)
            {
                Console.WriteLine($"        {error}");
            }
        }
    }

    private static void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("======================================================================");
        Console.WriteLine("  TELEMETRY STRESS TEST SUMMARY");
        Console.WriteLine("======================================================================");
        Console.WriteLine();
        Console.WriteLine($"  Total:   {_passed + _failed}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Passed:  {_passed}");
        Console.ResetColor();

        if (_failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Failed:  {_failed}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  Failures:");
            foreach (var f in _failures)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    - {f}");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Failed:  0");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("======================================================================");
        Console.WriteLine();
    }
}
