#nullable disable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEngine
{
    public readonly struct Vector2
    {
        public Vector2(float x, float y) { }
    }

    public enum TextAnchor
    {
        MiddleCenter
    }

    public sealed class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public bool wordWrap { get; set; }
    }

    public sealed class GUIContent
    {
        public GUIContent(string text) { }
        public GUIContent(string text, string tooltip) { }
    }

    public sealed class GUILayoutOption { }

    public static class GUILayout
    {
        public static void FlexibleSpace() { }
        public static void Space(float pixels) { }
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static GUILayoutOption Height(float value) => new GUILayoutOption();
        public static GUILayoutOption MinHeight(float value) => new GUILayoutOption();
        public static GUILayoutOption Width(float value) => new GUILayoutOption();
    }
}

namespace UnityEngine.UIElements
{
    public class VisualElement { }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public int priority { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CustomEditor : Attribute
    {
        public CustomEditor(Type inspectedType) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SettingsProviderAttribute : Attribute { }

    public class EditorWindow : ScriptableObject
    {
        public Vector2 minSize { get; set; }
        public static T GetWindow<T>(string title) where T : EditorWindow, new() => new T();
        public void Show() { }
        public void Repaint() { }
    }

    public class Editor
    {
        public SerializedObject serializedObject { get; } = new SerializedObject(new ScriptableObject());
        public virtual void OnInspectorGUI() { }
    }

    public sealed class SerializedObject
    {
        public SerializedObject(UnityEngine.Object target) { }
        public void Update() { }
        public void ApplyModifiedProperties() { }
        public SerializedProperty FindProperty(string propertyPath) => new SerializedProperty();
    }

    public sealed class SerializedProperty
    {
        public string stringValue { get; set; } = string.Empty;
        public int intValue { get; set; }
        public float floatValue { get; set; }
        public bool boolValue { get; set; }
    }

    public enum MessageType
    {
        None,
        Info,
        Warning,
        Error
    }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel { get; } = new GUIStyle();
        public static GUIStyle miniLabel { get; } = new GUIStyle();
        public static GUIStyle textArea { get; } = new GUIStyle();
    }

    public static class EditorGUILayout
    {
        public sealed class HorizontalScope : IDisposable
        {
            public void Dispose() { }
        }

        public static Vector2 BeginScrollView(Vector2 scrollPosition) => scrollPosition;
        public static void EndScrollView() { }
        public static void BeginHorizontal() { }
        public static void EndHorizontal() { }
        public static void Space(float pixels) { }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { }
        public static void PropertyField(SerializedProperty property) { }
        public static void PropertyField(SerializedProperty property, GUIContent label) { }
        public static void HelpBox(string message, MessageType type) { }
        public static string PasswordField(string label, string password) => password;
        public static string PasswordField(GUIContent label, string password) => password;
        public static string TextArea(string text, GUIStyle style, params GUILayoutOption[] options) => text;
        public static bool BeginFoldoutHeaderGroup(bool foldout, string content) => foldout;
        public static void EndFoldoutHeaderGroup() { }
    }

    public static class EditorGUI
    {
        public sealed class IndentLevelScope : IDisposable
        {
            public void Dispose() { }
        }

        public static int indentLevel { get; set; }
        public static void BeginChangeCheck() { }
        public static bool EndChangeCheck() => false;
        public static void BeginDisabledGroup(bool disabled) { }
        public static void EndDisabledGroup() { }
    }

    public static class Selection
    {
        public static UnityEngine.Object activeObject { get; set; }
    }

    public static class EditorGUIUtility
    {
        public static void PingObject(UnityEngine.Object target) { }
    }

    public static class Undo
    {
        public static void RegisterCreatedObjectUndo(UnityEngine.Object target, string name) { }
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => false;
    }

    public static class AssetDatabase
    {
        public static bool IsValidFolder(string path) => true;
        public static string CreateFolder(string parentFolder, string newFolderName) => string.Empty;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static string[] FindAssets(string filter) => Array.Empty<string>();
        public static string[] FindAssets(string filter, string[] searchInFolders) => Array.Empty<string>();
        public static string GUIDToAssetPath(string guid) => string.Empty;
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => default;
    }

    public enum SettingsScope
    {
        User,
        Project
    }

    public class SettingsProvider
    {
        protected SettingsProvider(string path, SettingsScope scope) { }
        public string label { get; set; }
        public HashSet<string> keywords { get; set; }
        public virtual void OnActivate(string searchContext, VisualElement rootElement) { }
        public virtual void OnGUI(string searchContext) { }
    }
}

namespace UnityEditor.Build.Reporting
{
    public sealed class BuildReport { }
}

namespace UnityEditor.UnityLinker
{
    public sealed class UnityLinkerBuildPipelineData { }
}

namespace UnityEditor.Build
{
    using UnityEditor.Build.Reporting;
    using UnityEditor.UnityLinker;

    public interface IUnityLinkerProcessor
    {
        int callbackOrder { get; }
        string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data);
    }
}

namespace UnityEditor.PackageManager
{
    public sealed class PackageInfo
    {
        public string resolvedPath { get; set; }
        public static PackageInfo FindForAssetPath(string assetPath) => default;
    }
}
