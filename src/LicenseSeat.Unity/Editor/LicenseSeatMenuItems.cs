#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using LicenseSeat.Unity;

namespace LicenseSeat.Editor
{
    /// <summary>
    /// Menu items for LicenseSeat SDK.
    /// </summary>
    public static class LicenseSeatMenuItems
    {
        private const string MenuPrefix = "Window/LicenseSeat/";
        private const string CreateMenuPrefix = "Assets/Create/LicenseSeat/";
        private const string GameObjectMenuPrefix = "GameObject/LicenseSeat/";

        [MenuItem(MenuPrefix + "Documentation", priority = 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://licenseseat.com/docs/sdk/unity");
        }

        [MenuItem(MenuPrefix + "Dashboard", priority = 101)]
        public static void OpenDashboard()
        {
            Application.OpenURL("https://licenseseat.com/dashboard");
        }

        [MenuItem(MenuPrefix + "Support", priority = 102)]
        public static void OpenSupport()
        {
            Application.OpenURL("https://github.com/licenseseat/licenseseat-csharp/issues");
        }

        [MenuItem(CreateMenuPrefix + "Settings", priority = 1)]
        public static void CreateSettings()
        {
            var settings = LicenseSeatSettings.GetOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem(GameObjectMenuPrefix + "LicenseSeat Manager", priority = 10)]
        public static void CreateLicenseSeatManager()
        {
            // Check if manager already exists
            var existing = Object.FindObjectOfType<LicenseSeatManager>();
            if (existing != null)
            {
                Debug.LogWarning("[LicenseSeat SDK] A LicenseSeatManager already exists in the scene.");
                Selection.activeObject = existing.gameObject;
                return;
            }

            // Create new GameObject with manager
            var go = new GameObject("LicenseSeat Manager");
            go.AddComponent<LicenseSeatManager>();

            Undo.RegisterCreatedObjectUndo(go, "Create LicenseSeat Manager");
            Selection.activeObject = go;

            Debug.Log("[LicenseSeat SDK] Created LicenseSeat Manager. Configure settings via Window > LicenseSeat > Settings");
        }

        [MenuItem(MenuPrefix + "Create Manager in Scene", priority = 50)]
        public static void CreateManagerFromMenu()
        {
            CreateLicenseSeatManager();
        }

        [MenuItem(MenuPrefix + "Select Settings Asset", priority = 51)]
        public static void SelectSettings()
        {
            var settings = LicenseSeatSettings.Load();
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            else
            {
                if (EditorUtility.DisplayDialog(
                    "No Settings Found",
                    "No LicenseSeat settings asset found. Would you like to create one?",
                    "Create",
                    "Cancel"))
                {
                    CreateSettings();
                }
            }
        }
    }
}
#endif
