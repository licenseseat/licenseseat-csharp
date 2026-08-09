#nullable enable
#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LicenseSeat.Unity;

namespace LicenseSeat.Editor
{
    /// <summary>
    /// Editor window for LicenseSeat SDK configuration and testing.
    /// </summary>
    public class LicenseSeatSettingsWindow : EditorWindow
    {
        private LicenseSeatSettings? _settings;
        private SerializedObject? _serializedSettings;
        private Vector2 _scrollPosition;
        private string _testLicenseKey = "";
        private string _testResult = "";
        private bool _isTesting;
        private CancellationTokenSource? _lifetimeCancellation;

        [MenuItem("Window/LicenseSeat/Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<LicenseSeatSettingsWindow>("LicenseSeat SDK");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _lifetimeCancellation = new CancellationTokenSource();
            LoadSettings();
        }

        private void OnDisable()
        {
            _lifetimeCancellation?.Cancel();
            _lifetimeCancellation?.Dispose();
            _lifetimeCancellation = null;
            _testLicenseKey = "";
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
            if (_settings == null || _serializedSettings == null)
            {
                EditorGUILayout.HelpBox("Settings could not be loaded safely.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            _serializedSettings.Update();

            EditorGUI.BeginChangeCheck();

            var apiKey = _serializedSettings.FindProperty("apiKey");
            apiKey.stringValue = EditorGUILayout.PasswordField(
                new GUIContent("Restricted SDK Key"),
                apiKey.stringValue);
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("productId"), new GUIContent("Product Slug"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("baseUrl"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("autoValidateInterval"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("offlineFallbackMode"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("maxOfflineDays"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("enableDebugLogging"));
            EditorGUILayout.HelpBox(
                "Player settings are inspectable. Use only a client key scoped to licenses:validate.",
                MessageType.Warning);

            if (EditorGUI.EndChangeCheck())
            {
                _serializedSettings.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(10);

            // Validation status
            if (!_settings.IsValid)
            {
                EditorGUILayout.HelpBox("A restricted SDK key and product slug are required.", MessageType.Warning);
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
            if (_settings == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "This makes a real API request. Use a restricted licenses:validate key. " +
                "The entered license key is kept only while this window is open and is never shown in results.",
                MessageType.Warning);

            EditorGUILayout.Space(5);

            _testLicenseKey = EditorGUILayout.PasswordField("License Key", _testLicenseKey);

            EditorGUILayout.Space(5);

            EditorGUI.BeginDisabledGroup(_isTesting || !_settings.IsValid || string.IsNullOrWhiteSpace(_testLicenseKey));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Validate"))
            {
                _ = TestValidateAsync(GetLifetimeToken());
            }
            if (GUILayout.Button("Test Activate"))
            {
                _ = TestActivateAsync(GetLifetimeToken());
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

        private async Task TestValidateAsync(CancellationToken cancellationToken)
        {
            _isTesting = true;
            _testResult = "Validating...";
            Repaint();

            try
            {
                var options = _settings!.ToClientOptions();
                using var client = new LicenseSeatClient(options);

                var result = await client.ValidateAsync(
                    _testLicenseKey,
                    cancellationToken: cancellationToken);

                _testResult = $"Validation Result:\n" +
                              $"  Valid: {result.Valid}\n" +
                              $"  Offline: {result.Offline}\n";

                if (result.License != null)
                {
                    _testResult += $"  Status: {result.License.Status}\n" +
                                   $"  Plan: {result.License.PlanKey}";
                }
            }
            catch (OperationCanceledException)
            {
                _testResult = "Canceled.";
            }
            catch (Exception)
            {
                _testResult = "Validation failed. See bounded SDK diagnostics for details.";
            }
            finally
            {
                _isTesting = false;
                _testLicenseKey = "";
                Repaint();
            }
        }

        private async Task TestActivateAsync(CancellationToken cancellationToken)
        {
            _isTesting = true;
            _testResult = "Activating...";
            Repaint();

            try
            {
                var options = _settings!.ToClientOptions();
                using var client = new LicenseSeatClient(options);

                var license = await client.ActivateAsync(
                    _testLicenseKey,
                    cancellationToken: cancellationToken);

                _testResult = $"Activation Successful!\n" +
                              $"  Status: {license.Status}\n" +
                              $"  Plan: {license.PlanKey}\n" +
                              $"  Seat Limit: {license.SeatLimit}";
            }
            catch (OperationCanceledException)
            {
                _testResult = "Canceled.";
            }
            catch (Exception)
            {
                _testResult = "Activation failed. See bounded SDK diagnostics for details.";
            }
            finally
            {
                _isTesting = false;
                _testLicenseKey = "";
                Repaint();
            }
        }

        private CancellationToken GetLifetimeToken()
        {
            if (_lifetimeCancellation == null)
            {
                throw new InvalidOperationException("The settings window is not active.");
            }

            return _lifetimeCancellation.Token;
        }
    }
}
#endif
