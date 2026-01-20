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
            Assert.That(_settings.BaseUrl, Is.EqualTo("https://api.licenseseat.com"));
            Assert.That(_settings.AutoValidateInterval, Is.EqualTo(0));
            Assert.That(_settings.MaxOfflineDays, Is.EqualTo(7));
            Assert.That(_settings.EnableDebugLogging, Is.False);
        }

        [Test]
        public void Settings_ToClientOptions_ReturnsValidOptions()
        {
            _settings.ApiKey = "test-api-key";
            _settings.ProductId = "test-product";

            var options = _settings.ToClientOptions();

            Assert.That(options, Is.Not.Null);
            Assert.That(options.ApiKey, Is.EqualTo("test-api-key"));
            Assert.That(options.ProductId, Is.EqualTo("test-product"));
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
            _settings.OfflineFallbackMode = OfflineFallbackMode.CacheFirst;

            var options = _settings.ToClientOptions();

            Assert.That(options.OfflineFallbackMode, Is.EqualTo(OfflineFallbackMode.CacheFirst));
        }
    }
}
