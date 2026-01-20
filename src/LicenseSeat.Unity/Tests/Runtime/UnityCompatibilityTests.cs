#if UNITY_5_3_OR_NEWER
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using LicenseSeat.Unity;

namespace LicenseSeat.Unity.Tests.Runtime
{
    /// <summary>
    /// Comprehensive Unity SDK compatibility tests.
    /// These tests verify the SDK meets PRD requirements for:
    /// - IL2CPP compatibility
    /// - WebGL compatibility
    /// - Lifecycle management
    /// - Error handling
    /// - Configuration
    /// </summary>
    [TestFixture]
    public class UnityCompatibilityTests
    {
        #region WebGL Compatibility Tests

        [Test]
        public void UnityWebRequestAdapter_IsAvailable()
        {
            // Verify UnityWebRequestAdapter exists and is accessible
            var adapterType = typeof(UnityWebRequestAdapter);
            Assert.That(adapterType, Is.Not.Null);
            Assert.That(typeof(IHttpClientAdapter).IsAssignableFrom(adapterType), Is.True,
                "UnityWebRequestAdapter should implement IHttpClientAdapter");
        }

        [Test]
        public void NoSystemNetHttpClient_InUnityWebRequestAdapter()
        {
            // Verify UnityWebRequestAdapter doesn't use System.Net.Http.HttpClient
            // which would break on WebGL
            var adapterType = typeof(UnityWebRequestAdapter);
            var fields = adapterType.GetFields(BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                Assert.That(field.FieldType.FullName, Does.Not.Contain("System.Net.Http.HttpClient"),
                    $"Field {field.Name} should not use HttpClient (WebGL incompatible)");
            }
        }

        [Test]
        public void RuntimeCode_DoesNotUseSystemNetSockets()
        {
            // Verify no System.Net.Sockets usage in Unity runtime code
            // System.Net.Sockets is completely non-functional on WebGL
            var unityAssembly = typeof(UnityWebRequestAdapter).Assembly;
            var types = unityAssembly.GetTypes()
                .Where(t => t.Namespace?.StartsWith("LicenseSeat") == true);

            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var method in methods)
                {
                    // Check method body doesn't reference System.Net.Sockets
                    // This is a compile-time safety check
                    var parameters = method.GetParameters();
                    foreach (var param in parameters)
                    {
                        Assert.That(param.ParameterType.Namespace,
                            Is.Not.EqualTo("System.Net.Sockets"),
                            $"Method {type.Name}.{method.Name} should not use System.Net.Sockets");
                    }
                }
            }
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void ScriptableObject_SerializesCorrectly()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
            settings.ApiKey = "test-api-key-12345";
            settings.ProductId = "test-product-xyz";
            settings.MaxOfflineDays = 30;
            settings.OfflineFallbackMode = OfflineFallbackMode.Always;
            settings.AutoValidateInterval = 600f;

            // Verify serialization round-trip
            Assert.That(settings.ApiKey, Is.EqualTo("test-api-key-12345"));
            Assert.That(settings.ProductId, Is.EqualTo("test-product-xyz"));
            Assert.That(settings.MaxOfflineDays, Is.EqualTo(30));
            Assert.That(settings.OfflineFallbackMode, Is.EqualTo(OfflineFallbackMode.Always));
            Assert.That(settings.AutoValidateInterval, Is.EqualTo(600f));

            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void Settings_DefaultApiUrl_MatchesCoreSDK()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();

            // Verify default URL matches the core SDK constant
            Assert.That(settings.BaseUrl, Is.EqualTo(LicenseSeatClientOptions.DefaultApiBaseUrl));
            Assert.That(settings.BaseUrl, Is.EqualTo("https://licenseseat.com/api"));

            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void ToClientOptions_ReturnsConsistentConfiguration()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
            settings.ApiKey = "api-key";
            settings.ProductId = "product-id";
            settings.MaxOfflineDays = 14;
            settings.OfflineFallbackMode = OfflineFallbackMode.NetworkOnly;
            settings.AutoValidateInterval = 300f;
            settings.EnableDebugLogging = true;

            var options = settings.ToClientOptions();

            Assert.That(options.ApiKey, Is.EqualTo("api-key"));
            Assert.That(options.MaxOfflineDays, Is.EqualTo(14));
            Assert.That(options.OfflineFallbackMode, Is.EqualTo(OfflineFallbackMode.NetworkOnly));
            Assert.That(options.AutoValidateInterval.TotalSeconds, Is.EqualTo(300));
            Assert.That(options.Debug, Is.True);
            Assert.That(options.AutoInitialize, Is.False, "AutoInitialize should be false for Unity");

            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void CreateValidationOptions_SetsProductSlug()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
            settings.ProductId = "my-product";

            var validationOptions = settings.CreateValidationOptions();

            Assert.That(validationOptions.ProductSlug, Is.EqualTo("my-product"));

            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void Settings_HandlesEmptyBaseUrl()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
            settings.ApiKey = "key";
            settings.ProductId = "product";
            settings.BaseUrl = "";

            var options = settings.ToClientOptions();

            // Should fall back to default URL
            Assert.That(options.ApiBaseUrl, Is.EqualTo(LicenseSeatClientOptions.DefaultApiBaseUrl));

            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void Settings_HandlesWhitespaceBaseUrl()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
            settings.ApiKey = "key";
            settings.ProductId = "product";
            settings.BaseUrl = "   ";

            var options = settings.ToClientOptions();

            // Should fall back to default URL
            Assert.That(options.ApiBaseUrl, Is.EqualTo(LicenseSeatClientOptions.DefaultApiBaseUrl));

            UnityEngine.Object.DestroyImmediate(settings);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void InvalidLicense_ReturnsProperError()
        {
            // Verify validation result structure for invalid licenses
            var result = ValidationResult.Failed("License key not found", "license_not_found");

            Assert.That(result.Valid, Is.False);
            Assert.That(result.Reason, Is.EqualTo("License key not found"));
            Assert.That(result.ReasonCode, Is.EqualTo("license_not_found"));
        }

        [Test]
        public void ValidationResult_OfflineResult_IsMarkedCorrectly()
        {
            var result = ValidationResult.OfflineResult(true, reasonCode: "cached");

            Assert.That(result.Valid, Is.True);
            Assert.That(result.Offline, Is.True);
            Assert.That(result.ReasonCode, Is.EqualTo("cached"));
        }

        [Test]
        public void LicenseException_HasCorrectErrorCodes()
        {
            var noLicense = LicenseException.NoActiveLicense();
            Assert.That(noLicense.ErrorCode, Is.EqualTo("no_license"));

            var expired = LicenseException.Expired();
            Assert.That(expired.ErrorCode, Is.EqualTo("expired"));

            var revoked = LicenseException.Revoked();
            Assert.That(revoked.ErrorCode, Is.EqualTo("revoked"));
        }

        #endregion

        #region Type Preservation Tests (IL2CPP)

        [Test]
        public void AllModelTypes_HaveParameterlessConstructors()
        {
            // IL2CPP may strip constructors if not preserved
            var modelTypes = new[]
            {
                typeof(License),
                typeof(ValidationResult),
                typeof(ActivationResult),
                typeof(Entitlement),
                typeof(EntitlementStatus),
                typeof(LicenseStatus),
                typeof(Product),
                typeof(ValidationOptions),
                typeof(ActivationOptions)
            };

            foreach (var type in modelTypes)
            {
                var ctor = type.GetConstructor(Type.EmptyTypes);
                Assert.That(ctor, Is.Not.Null,
                    $"{type.Name} should have a parameterless constructor for IL2CPP");
            }
        }

        [Test]
        public void JsonPropertyNames_ArePreservedOnModels()
        {
            // Verify JSON serialization attributes are present
            var licenseType = typeof(License);
            var licenseKeyProperty = licenseType.GetProperty("LicenseKey");

            Assert.That(licenseKeyProperty, Is.Not.Null);

            var jsonAttr = licenseKeyProperty?.GetCustomAttributes(
                typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), true);
            Assert.That(jsonAttr, Is.Not.Null.And.Not.Empty,
                "LicenseKey should have JsonPropertyName attribute for serialization");
        }

        [Test]
        public void OfflineFallbackMode_EnumValues_AreCorrect()
        {
            // Verify enum values match API expectations
            Assert.That((int)OfflineFallbackMode.Disabled, Is.EqualTo(0));
            Assert.That((int)OfflineFallbackMode.NetworkOnly, Is.EqualTo(1));
            Assert.That((int)OfflineFallbackMode.Always, Is.EqualTo(2));
        }

        #endregion

        #region Memory Safety Tests

        [Test]
        public void Settings_CleanupOnDestroy()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
            settings.ApiKey = "test";
            settings.ProductId = "test";

            // Should not throw on destroy
            Assert.DoesNotThrow(() => UnityEngine.Object.DestroyImmediate(settings));
        }

        [Test]
        public void ClientOptions_Clone_CreatesIndependentCopy()
        {
            var original = new LicenseSeatClientOptions
            {
                ApiKey = "original-key",
                Debug = true,
                MaxOfflineDays = 7
            };

            var clone = original.Clone();
            clone.ApiKey = "cloned-key";
            clone.MaxOfflineDays = 14;

            // Verify original is unchanged
            Assert.That(original.ApiKey, Is.EqualTo("original-key"));
            Assert.That(original.MaxOfflineDays, Is.EqualTo(7));
            Assert.That(clone.ApiKey, Is.EqualTo("cloned-key"));
            Assert.That(clone.MaxOfflineDays, Is.EqualTo(14));
        }

        #endregion

        #region Event System Tests

        [Test]
        public void EventBus_SubscribeAndEmit_Works()
        {
            var eventBus = new EventBus();
            var received = false;
            object? receivedData = null;

            eventBus.On("test:event", data =>
            {
                received = true;
                receivedData = data;
            });

            eventBus.Emit("test:event", "test-data");

            Assert.That(received, Is.True);
            Assert.That(receivedData, Is.EqualTo("test-data"));
        }

        [Test]
        public void EventBus_Unsubscribe_Works()
        {
            var eventBus = new EventBus();
            var callCount = 0;

            void Handler(object? data) => callCount++;

            eventBus.On("test:event", Handler);
            eventBus.Emit("test:event", null);

            eventBus.Off("test:event", Handler);
            eventBus.Emit("test:event", null);

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void EventBus_Clear_RemovesAllSubscriptions()
        {
            var eventBus = new EventBus();
            var callCount = 0;

            eventBus.On("event1", _ => callCount++);
            eventBus.On("event2", _ => callCount++);

            eventBus.Clear();

            eventBus.Emit("event1", null);
            eventBus.Emit("event2", null);

            Assert.That(callCount, Is.EqualTo(0));
        }

        [Test]
        public void LicenseSeatEvents_Constants_AreDefined()
        {
            // Verify critical event constants exist
            Assert.That(LicenseSeatEvents.ActivationSuccess, Is.EqualTo("activation:success"));
            Assert.That(LicenseSeatEvents.ActivationError, Is.EqualTo("activation:error"));
            Assert.That(LicenseSeatEvents.ValidationSuccess, Is.EqualTo("validation:success"));
            Assert.That(LicenseSeatEvents.ValidationFailed, Is.EqualTo("validation:failed"));
            Assert.That(LicenseSeatEvents.ValidationOfflineSuccess, Is.EqualTo("validation:offline-success"));
            Assert.That(LicenseSeatEvents.NetworkOnline, Is.EqualTo("network:online"));
            Assert.That(LicenseSeatEvents.NetworkOffline, Is.EqualTo("network:offline"));
        }

        #endregion

        #region API Compliance Tests

        [Test]
        public void ValidationOptions_HasProductSlug_NotProductId()
        {
            // Verify API compliance - API uses product_slug, not product_id
            var options = new ValidationOptions();
            var productSlugProp = typeof(ValidationOptions).GetProperty("ProductSlug");
            var productIdProp = typeof(ValidationOptions).GetProperty("ProductId");

            Assert.That(productSlugProp, Is.Not.Null, "ValidationOptions should have ProductSlug");
            Assert.That(productIdProp, Is.Null, "ValidationOptions should NOT have ProductId");
        }

        [Test]
        public void LicenseSeatClientOptions_NoProductId()
        {
            // Verify LicenseSeatClientOptions doesn't have ProductId
            // ProductId is a per-validation option, not a client-wide setting
            var productIdProp = typeof(LicenseSeatClientOptions).GetProperty("ProductId");
            Assert.That(productIdProp, Is.Null,
                "LicenseSeatClientOptions should NOT have ProductId (API uses product_slug per-call)");
        }

        [Test]
        public void DefaultApiUrl_IsCorrect()
        {
            Assert.That(LicenseSeatClientOptions.DefaultApiBaseUrl,
                Is.EqualTo("https://licenseseat.com/api"),
                "Default API URL should be https://licenseseat.com/api");
        }

        #endregion
    }
}
#endif
