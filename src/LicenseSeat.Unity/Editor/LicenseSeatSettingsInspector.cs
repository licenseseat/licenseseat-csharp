#nullable enable
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LicenseSeat.Unity;

namespace LicenseSeat.Editor
{
    /// <summary>
    /// Custom inspector for LicenseSeatSettings ScriptableObject.
    /// Provides a better editing experience with organized sections and validation.
    /// </summary>
    [CustomEditor(typeof(LicenseSeatSettings))]
    public class LicenseSeatSettingsInspector : UnityEditor.Editor
    {
        private SerializedProperty? _apiKey;
        private SerializedProperty? _productId;
        private SerializedProperty? _baseUrl;
        private SerializedProperty? _autoValidateInterval;
        private SerializedProperty? _offlineFallbackMode;
        private SerializedProperty? _maxOfflineDays;
        private SerializedProperty? _enableDebugLogging;

        private bool _showApiConfig = true;
        private bool _showValidationConfig = true;
        private bool _showOfflineConfig = true;
        private bool _showDebugConfig = true;

        private void OnEnable()
        {
            _apiKey = serializedObject.FindProperty("apiKey");
            _productId = serializedObject.FindProperty("productId");
            _baseUrl = serializedObject.FindProperty("baseUrl");
            _autoValidateInterval = serializedObject.FindProperty("autoValidateInterval");
            _offlineFallbackMode = serializedObject.FindProperty("offlineFallbackMode");
            _maxOfflineDays = serializedObject.FindProperty("maxOfflineDays");
            _enableDebugLogging = serializedObject.FindProperty("enableDebugLogging");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Header
            EditorGUILayout.Space(5);
            DrawHeader();
            EditorGUILayout.Space(10);

            // Validation warnings
            DrawValidationWarnings();

            // API Configuration Section
            _showApiConfig = EditorGUILayout.BeginFoldoutHeaderGroup(_showApiConfig, "API Configuration");
            if (_showApiConfig)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawApiConfigSection();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // Validation Settings Section
            _showValidationConfig = EditorGUILayout.BeginFoldoutHeaderGroup(_showValidationConfig, "Validation Settings");
            if (_showValidationConfig)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawValidationSection();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // Offline Settings Section
            _showOfflineConfig = EditorGUILayout.BeginFoldoutHeaderGroup(_showOfflineConfig, "Offline Settings");
            if (_showOfflineConfig)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawOfflineSection();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(5);

            // Debug Settings Section
            _showDebugConfig = EditorGUILayout.BeginFoldoutHeaderGroup(_showDebugConfig, "Debug Settings");
            if (_showDebugConfig)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawDebugSection();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(10);

            // Action buttons
            DrawActionButtons();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                var headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };

                EditorGUILayout.LabelField("LicenseSeat Settings", headerStyle);

                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Configure your licensing SDK", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawValidationWarnings()
        {
            var hasWarnings = false;

            if (_apiKey != null && string.IsNullOrEmpty(_apiKey.stringValue))
            {
                EditorGUILayout.HelpBox("API Key is required for the SDK to function.", MessageType.Warning);
                hasWarnings = true;
            }

            if (_productId != null && string.IsNullOrEmpty(_productId.stringValue))
            {
                EditorGUILayout.HelpBox("Product slug is required for license validation.", MessageType.Warning);
                hasWarnings = true;
            }

            if (hasWarnings)
            {
                EditorGUILayout.Space(5);
            }
        }

        private void DrawApiConfigSection()
        {
            if (_apiKey == null || _productId == null || _baseUrl == null) return;

            _apiKey.stringValue = EditorGUILayout.PasswordField(
                new GUIContent(
                    "Restricted SDK Key",
                    "Use only a client key scoped to licenses:validate; never an administrator/server key."),
                _apiKey.stringValue);
            EditorGUILayout.HelpBox(
                "Settings assets are embedded in player builds. This value must be a restricted client credential.",
                MessageType.Warning);

            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(_productId, new GUIContent("Product Slug", "Your product slug"));
            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(_baseUrl, new GUIContent("Base URL", "API base URL (leave default unless self-hosting)"));
        }

        private void DrawValidationSection()
        {
            if (_autoValidateInterval == null) return;

            EditorGUILayout.PropertyField(_autoValidateInterval, new GUIContent("Auto-Validate Interval", "Interval in seconds between automatic validations (0 = disabled)"));

            if (_autoValidateInterval.floatValue > 0 && _autoValidateInterval.floatValue < 60)
            {
                EditorGUILayout.HelpBox("Consider using a longer interval to reduce API calls.", MessageType.Info);
            }
        }

        private void DrawOfflineSection()
        {
            if (_offlineFallbackMode == null || _maxOfflineDays == null) return;

            EditorGUILayout.PropertyField(_offlineFallbackMode, new GUIContent("Offline Fallback Mode", "How to handle license validation when offline"));
            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(_maxOfflineDays, new GUIContent("Max Offline Days", "Maximum days a cached license is valid while offline"));

            if (_maxOfflineDays.intValue > 30)
            {
                EditorGUILayout.HelpBox("Long offline periods may increase piracy risk.", MessageType.Info);
            }
        }

        private void DrawDebugSection()
        {
            if (_enableDebugLogging == null) return;

            EditorGUILayout.PropertyField(_enableDebugLogging, new GUIContent("Enable Debug Logging", "Log detailed SDK operations to the console"));

            if (_enableDebugLogging.boolValue)
            {
                EditorGUILayout.HelpBox("Debug logging is enabled. Disable for release builds.", MessageType.Info);
            }
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Editor Window"))
                {
                    LicenseSeatSettingsWindow.ShowWindow();
                }

                if (GUILayout.Button("Documentation"))
                {
                    Application.OpenURL("https://licenseseat.com/docs/sdk/unity");
                }
            }
        }

    }
}
#endif
