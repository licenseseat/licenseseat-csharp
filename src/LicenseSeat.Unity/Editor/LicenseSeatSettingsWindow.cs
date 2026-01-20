#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace LicenseSeat.Editor
{
    /// <summary>
    /// Editor window for LicenseSeat SDK configuration and testing.
    /// </summary>
    public class LicenseSeatSettingsWindow : EditorWindow
    {
        private LicenseSeatSettings _settings;
        private SerializedObject _serializedSettings;
        private Vector2 _scrollPosition;
        private string _testLicenseKey = "";
        private string _testResult = "";
        private bool _isTesting;

        [MenuItem("Window/LicenseSeat/Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<LicenseSeatSettingsWindow>("LicenseSeat SDK");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            _settings = LicenseSeatSettings.Load();
            if (_settings != null)
            {
                _serializedSettings = new SerializedObject(_settings);
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            if (_settings == null)
            {
                DrawNoSettingsUI();
            }
            else
            {
                DrawSettingsUI();
                EditorGUILayout.Space(20);
                DrawTestingUI();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("LicenseSeat SDK", headerStyle, GUILayout.Height(30));

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Unity Integration", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoSettingsUI()
        {
            EditorGUILayout.HelpBox(
                "No LicenseSeat settings found.\n\nCreate a settings asset to configure the SDK.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create Settings Asset", GUILayout.Height(30)))
            {
                _settings = LicenseSeatSettings.GetOrCreateSettings();
                _serializedSettings = new SerializedObject(_settings);
                Selection.activeObject = _settings;
            }
        }

        private void DrawSettingsUI()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            _serializedSettings?.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_apiKey"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_apiBaseUrl"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_autoValidateIntervalMinutes"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_httpTimeoutSeconds"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_offlineFallbackMode"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_maxOfflineDays"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_debugLogging"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("_storagePrefix"));

            if (EditorGUI.EndChangeCheck())
            {
                _serializedSettings?.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(10);

            // Validation status
            if (!_settings.IsValid())
            {
                EditorGUILayout.HelpBox("API Key is required.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Configuration is valid.", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Settings Asset"))
            {
                Selection.activeObject = _settings;
                EditorGUIUtility.PingObject(_settings);
            }
            if (GUILayout.Button("Refresh"))
            {
                LoadSettings();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTestingUI()
        {
            EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Test license operations in the editor. Note: This creates a real API connection.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            _testLicenseKey = EditorGUILayout.TextField("License Key", _testLicenseKey);

            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(_isTesting || !_settings.IsValid() || string.IsNullOrWhiteSpace(_testLicenseKey));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Validate"))
            {
                TestValidate();
            }
            if (GUILayout.Button("Test Activate"))
            {
                TestActivate();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_testResult))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);

                var resultStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true
                };
                EditorGUILayout.TextArea(_testResult, resultStyle, GUILayout.MinHeight(100));
            }
        }

        private async void TestValidate()
        {
            _isTesting = true;
            _testResult = "Validating...";
            Repaint();

            try
            {
                var options = _settings.ToClientOptions();
                using var client = new LicenseSeatClient(options);

                var result = await client.ValidateAsync(_testLicenseKey);

                _testResult = $"Validation Result:\n" +
                              $"  Valid: {result.Valid}\n" +
                              $"  Offline: {result.Offline}\n";

                if (result.License != null)
                {
                    _testResult += $"  License Key: {result.License.LicenseKey}\n" +
                                   $"  Status: {result.License.Status}\n" +
                                   $"  Plan: {result.License.PlanKey}";
                }
            }
            catch (System.Exception ex)
            {
                _testResult = $"Error: {ex.Message}";
            }
            finally
            {
                _isTesting = false;
                Repaint();
            }
        }

        private async void TestActivate()
        {
            _isTesting = true;
            _testResult = "Activating...";
            Repaint();

            try
            {
                var options = _settings.ToClientOptions();
                using var client = new LicenseSeatClient(options);

                var license = await client.ActivateAsync(_testLicenseKey);

                _testResult = $"Activation Successful!\n" +
                              $"  License Key: {license.LicenseKey}\n" +
                              $"  Status: {license.Status}\n" +
                              $"  Plan: {license.PlanKey}\n" +
                              $"  Seat Limit: {license.SeatLimit}";
            }
            catch (System.Exception ex)
            {
                _testResult = $"Error: {ex.Message}";
            }
            finally
            {
                _isTesting = false;
                Repaint();
            }
        }
    }
}
#endif
