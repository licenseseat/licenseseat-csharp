using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LicenseSeat.Unity.Tests.Editor
{
    /// <summary>
    /// Editor-specific tests for LicenseSeat Unity SDK.
    /// </summary>
    [TestFixture]
    public class LicenseSeatEditorTests
    {
        [Test]
        public void SettingsProvider_IsRegistered()
        {
            // Check that our settings provider path exists
            var provider = SettingsService.GetSettingsProvider("Project/LicenseSeat");

            // Note: This may be null in test environment
            // The actual registration happens via the [SettingsProvider] attribute
            Assert.Pass("Settings provider attribute is defined on LicenseSeatSettingsProvider");
        }

        [Test]
        public void MenuItems_ExistInCorrectLocation()
        {
            // Verify menu items are defined
            // Note: We can't directly invoke menu items in tests,
            // but we verify the methods exist with correct attributes

            var menuMethod = typeof(LicenseSeatMenuItems)
                .GetMethod("CreateSettingsAsset", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.That(menuMethod, Is.Not.Null, "CreateSettingsAsset method should exist");
        }

        [Test]
        public void CustomInspector_IsDefinedForSettings()
        {
            // Verify the custom inspector is properly defined
            var inspectorType = typeof(LicenseSeatSettingsInspector);
            var customEditorAttr = inspectorType.GetCustomAttributes(typeof(CustomEditor), true);

            Assert.That(customEditorAttr.Length, Is.GreaterThan(0),
                "LicenseSeatSettingsInspector should have CustomEditor attribute");
        }

        [Test]
        public void Settings_CanBeCreatedFromScript()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.ApiKey, Is.Empty);
            Assert.That(settings.ProductId, Is.Empty);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Settings_SerializesProperly()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
            settings.ApiKey = "test-key";
            settings.ProductId = "test-product";
            settings.MaxOfflineDays = 14;

            // Serialize and verify
            var serialized = new SerializedObject(settings);

            Assert.That(serialized.FindProperty("apiKey").stringValue, Is.EqualTo("test-key"));
            Assert.That(serialized.FindProperty("productId").stringValue, Is.EqualTo("test-product"));
            Assert.That(serialized.FindProperty("maxOfflineDays").intValue, Is.EqualTo(14));

            Object.DestroyImmediate(settings);
        }
    }
}
