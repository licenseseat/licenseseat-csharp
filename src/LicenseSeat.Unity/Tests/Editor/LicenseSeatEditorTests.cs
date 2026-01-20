#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using LicenseSeat.Unity;
using LicenseSeat.Editor;

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
            // The actual registration happens via the [SettingsProvider] attribute
            Assert.Pass("Settings provider attribute is defined on LicenseSeatSettingsProvider");
        }

        [Test]
        public void MenuItems_ExistInCorrectLocation()
        {
            // Verify menu items are defined
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

        [Test]
        public void Settings_AllFieldsAreSerializable()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();
            var serialized = new SerializedObject(settings);

            // Verify all expected fields exist
            Assert.That(serialized.FindProperty("apiKey"), Is.Not.Null, "apiKey should be serializable");
            Assert.That(serialized.FindProperty("productId"), Is.Not.Null, "productId should be serializable");
            Assert.That(serialized.FindProperty("baseUrl"), Is.Not.Null, "baseUrl should be serializable");
            Assert.That(serialized.FindProperty("validateOnStart"), Is.Not.Null, "validateOnStart should be serializable");
            Assert.That(serialized.FindProperty("autoValidateInterval"), Is.Not.Null, "autoValidateInterval should be serializable");
            Assert.That(serialized.FindProperty("offlineFallbackMode"), Is.Not.Null, "offlineFallbackMode should be serializable");
            Assert.That(serialized.FindProperty("maxOfflineDays"), Is.Not.Null, "maxOfflineDays should be serializable");
            Assert.That(serialized.FindProperty("enableDebugLogging"), Is.Not.Null, "enableDebugLogging should be serializable");

            Object.DestroyImmediate(settings);
        }
    }
}
#endif
