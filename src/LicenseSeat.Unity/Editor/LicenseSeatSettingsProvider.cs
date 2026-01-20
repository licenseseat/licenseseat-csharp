#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LicenseSeat.Unity.Editor
{
    /// <summary>
    /// Provides LicenseSeat settings in Unity's Project Settings window.
    /// Accessible via Edit > Project Settings > LicenseSeat.
    /// </summary>
    public class LicenseSeatSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/LicenseSeat";
        private const string DefaultSettingsAssetPath = "Assets/LicenseSeat/Resources/LicenseSeatSettings.asset";

        private SerializedObject? _serializedSettings;
        private LicenseSeatSettings? _settings;

        public LicenseSeatSettingsProvider(string path, SettingsScope scope)
            : base(path, scope)
        {
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _settings = FindOrCreateSettings();
            if (_settings != null)
            {
                _serializedSettings = new SerializedObject(_settings);
            }
        }

        public override void OnGUI(string searchContext)
        {
            if (_serializedSettings == null || _settings == null)
            {
                EditorGUILayout.HelpBox("Unable to find or create LicenseSeat settings asset.", MessageType.Error);
                if (GUILayout.Button("Create Settings Asset"))
                {
                    _settings = CreateSettingsAsset();
                    if (_settings != null)
                    {
                        _serializedSettings = new SerializedObject(_settings);
                    }
                }
                return;
            }

            _serializedSettings.Update();

            EditorGUILayout.Space(10);

            // Header
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("LicenseSeat SDK Configuration", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(10);

            // API Configuration
            EditorGUILayout.LabelField("API Configuration", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("apiKey"), new GUIContent("API Key"));
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("productId"), new GUIContent("Product ID"));
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("baseUrl"), new GUIContent("Base URL"));
            }

            EditorGUILayout.Space(10);

            // Validation Settings
            EditorGUILayout.LabelField("Validation Settings", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("validateOnStart"), new GUIContent("Validate On Start"));
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("autoValidateInterval"), new GUIContent("Auto-Validate Interval (sec)"));
            }

            EditorGUILayout.Space(10);

            // Offline Settings
            EditorGUILayout.LabelField("Offline Settings", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("offlineFallbackMode"), new GUIContent("Offline Fallback Mode"));
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("maxOfflineDays"), new GUIContent("Max Offline Days"));
            }

            EditorGUILayout.Space(10);

            // Debug Settings
            EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_serializedSettings.FindProperty("enableDebugLogging"), new GUIContent("Enable Debug Logging"));
            }

            EditorGUILayout.Space(20);

            // Actions
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Editor Window"))
                {
                    LicenseSeatSettingsWindow.ShowWindow();
                }

                if (GUILayout.Button("Select Settings Asset"))
                {
                    Selection.activeObject = _settings;
                    EditorGUIUtility.PingObject(_settings);
                }
            }

            EditorGUILayout.Space(10);

            // Documentation link
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("View Documentation", GUILayout.Width(150)))
                {
                    Application.OpenURL("https://docs.licenseseat.com/sdk/unity");
                }
                GUILayout.FlexibleSpace();
            }

            _serializedSettings.ApplyModifiedProperties();
        }

        private static LicenseSeatSettings? FindOrCreateSettings()
        {
            // First, try to find existing settings
            var settings = Resources.Load<LicenseSeatSettings>("LicenseSeatSettings");
            if (settings != null)
            {
                return settings;
            }

            // Search all assets
            var guids = AssetDatabase.FindAssets("t:LicenseSeatSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<LicenseSeatSettings>(path);
            }

            // No settings found, but don't auto-create
            return null;
        }

        private static LicenseSeatSettings? CreateSettingsAsset()
        {
            var settings = ScriptableObject.CreateInstance<LicenseSeatSettings>();

            // Ensure directory exists
            var directory = Path.GetDirectoryName(DefaultSettingsAssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(settings, DefaultSettingsAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LicenseSeat] Created settings asset at: {DefaultSettingsAssetPath}");
            return settings;
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LicenseSeatSettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "LicenseSeat",
                keywords = new HashSet<string>(new[]
                {
                    "License",
                    "LicenseSeat",
                    "Activation",
                    "API Key",
                    "Validation"
                })
            };

            return provider;
        }
    }
}
#endif
