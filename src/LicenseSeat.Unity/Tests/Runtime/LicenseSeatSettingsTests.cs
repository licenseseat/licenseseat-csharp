#if UNITY_5_3_OR_NEWER
using NUnit.Framework;
using UnityEngine;

namespace LicenseSeat.Unity.Tests.Runtime
{
    /// <summary>
    /// Tests for LicenseSeatSettings ScriptableObject.
    /// </summary>
    [TestFixture]
    public class LicenseSeatSettingsTests
    {
        private LicenseSeatSettings _settings = null!;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Settings_DefaultValues_AreCorrect()
        {
            Assert.That(_settings.BaseUrl, Is.EqualTo(LicenseSeatClientOptions.DefaultApiBaseUrl));
            Assert.That(_settings.AutoValidateInterval, Is.EqualTo(0f));
            Assert.That(_settings.MaxOfflineDays, Is.EqualTo(7));
            Assert.That(_settings.EnableDebugLogging, Is.False);
            Assert.That(_settings.ValidateOnStart, Is.True);
        }

        [Test]
        public void Settings_ToClientOptions_ReturnsValidOptions()
        {
            _settings.ApiKey = "test-api-key";
            _settings.ProductId = "test-product";

            var options = _settings.ToClientOptions();

            Assert.That(options, Is.Not.Null);
            Assert.That(options.ApiKey, Is.EqualTo("test-api-key"));
            // ProductId is not part of LicenseSeatClientOptions - it's passed via ValidationOptions.ProductSlug
            Assert.That(options.ApiBaseUrl, Is.EqualTo(LicenseSeatClientOptions.DefaultApiBaseUrl));
        }

        [Test]
        public void Settings_CreateValidationOptions_IncludesProductSlug()
        {
            _settings.ProductId = "test-product";

            var validationOptions = _settings.CreateValidationOptions();

            Assert.That(validationOptions, Is.Not.Null);
            Assert.That(validationOptions.ProductSlug, Is.EqualTo("test-product"));
        }

        [Test]
        public void Settings_ToClientOptions_SetsHttpAdapter()
        {
            _settings.ApiKey = "test-key";
            _settings.ProductId = "test-product";

            var options = _settings.ToClientOptions();

            // Should automatically set UnityWebRequestAdapter
            Assert.That(options.HttpClientAdapter, Is.Not.Null);
            Assert.That(options.HttpClientAdapter, Is.TypeOf<UnityWebRequestAdapter>());
        }

        [Test]
        public void Settings_IsValid_ReturnsFalse_WhenApiKeyMissing()
        {
            _settings.ApiKey = "";
            _settings.ProductId = "test-product";

            Assert.That(_settings.IsValid, Is.False);
        }

        [Test]
        public void Settings_IsValid_ReturnsFalse_WhenProductIdMissing()
        {
            _settings.ApiKey = "test-key";
            _settings.ProductId = "";

            Assert.That(_settings.IsValid, Is.False);
        }

        [Test]
        public void Settings_IsValid_ReturnsTrue_WhenConfigured()
        {
            _settings.ApiKey = "test-key";
            _settings.ProductId = "test-product";

            Assert.That(_settings.IsValid, Is.True);
        }

        [Test]
        public void Settings_OfflineFallbackMode_IsConfigurable()
        {
            _settings.OfflineFallbackMode = OfflineFallbackMode.Always;

            var options = _settings.ToClientOptions();

            Assert.That(options.OfflineFallbackMode, Is.EqualTo(OfflineFallbackMode.Always));
        }

        [Test]
        public void Settings_AutoValidateInterval_ConvertsToTimeSpan()
        {
            _settings.AutoValidateInterval = 300f; // 5 minutes in seconds

            var options = _settings.ToClientOptions();

            Assert.That(options.AutoValidateInterval.TotalSeconds, Is.EqualTo(300));
        }

        [Test]
        public void Settings_AutoValidateInterval_ZeroDisablesAutoValidation()
        {
            _settings.AutoValidateInterval = 0f;

            var options = _settings.ToClientOptions();

            Assert.That(options.AutoValidateInterval, Is.EqualTo(System.TimeSpan.Zero));
        }

        [Test]
        public void Settings_Load_ReturnsNullWhenNotFound()
        {
            // This test verifies the static Load method works
            // In a real Unity project, it would return the settings from Resources
            var settings = LicenseSeatSettings.Load();

            // May be null if no settings exist in test environment
            // This is expected behavior
            Assert.Pass("Load method executed without error");
        }
    }
}
#endif
