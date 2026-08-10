#nullable disable
using System;
using System.Collections;

namespace UnityEngine
{
    public class Object
    {
        public static T FindObjectOfType<T>() where T : Object => default;
        public static void Destroy(Object value) { }
        public static void DestroyImmediate(Object value) { }
        public static void DontDestroyOnLoad(Object value) { }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; } = new GameObject();
    }

    public class Behaviour : Component { }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => new Coroutine();
        public void InvokeRepeating(string methodName, float time, float repeatRate) { }
        public void CancelInvoke(string methodName) { }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public sealed class Coroutine { }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public T AddComponent<T>() where T : Component, new() => new T();
        public void SetActive(bool value) { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => default;
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
        public static void LogException(Exception exception) { }
    }

    public static class Application
    {
        public static event LogCallback logMessageReceived
        {
            add { }
            remove { }
        }
        public static NetworkReachability internetReachability { get; set; }
        public static void OpenURL(string url) { }
    }

    public static class SystemInfo
    {
        public static string deviceUniqueIdentifier { get; set; }
        public static string unsupportedIdentifier => "n/a";
    }

    public delegate void LogCallback(string condition, string stackTrace, LogType type);

    public enum LogType
    {
        Error,
        Assert,
        Warning,
        Log,
        Exception
    }

    public sealed class WaitForSeconds
    {
        public WaitForSeconds(float seconds) { }
    }

    public enum NetworkReachability
    {
        NotReachable,
        ReachableViaCarrierDataNetwork,
        ReachableViaLocalAreaNetwork
    }

    public readonly struct Color
    {
        public static Color white => default;
        public static Color yellow => default;
        public static Color red => default;
        public static Color green => default;
        public static Color gray => default;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinAttribute : Attribute
    {
        public MinAttribute(float minimum) { }
        public MinAttribute(int minimum) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HideInInspector : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddComponentMenu : Attribute
    {
        public AddComponentMenu(string menuName) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
        public int order { get; set; }
    }
}

namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UnityTestAttribute : Attribute { }
}

namespace UnityEngine.UI
{
    public sealed class InputField : UnityEngine.Component
    {
        public string text { get; set; } = string.Empty;
    }

    public sealed class Text : UnityEngine.Component
    {
        public string text { get; set; } = string.Empty;
        public UnityEngine.Color color { get; set; }
    }

    public sealed class Button : UnityEngine.Component
    {
        public bool interactable { get; set; }
        public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();

        public sealed class ButtonClickedEvent
        {
            public void AddListener(Action callback) { }
            public void RemoveListener(Action callback) { }
        }
    }
}

namespace UnityEngine.Networking
{
    public class DownloadHandler : IDisposable
    {
        public virtual void Dispose() { }
    }

    public class DownloadHandlerScript : DownloadHandler
    {
        protected DownloadHandlerScript(byte[] preallocatedBuffer) { }
        protected virtual bool ReceiveData(byte[] data, int dataLength) => true;
    }

    public class UploadHandler : IDisposable
    {
        public virtual void Dispose() { }
    }

    public sealed class UploadHandlerRaw : UploadHandler
    {
        public UploadHandlerRaw(byte[] data) { }
    }

    public sealed class UnityWebRequest : IDisposable
    {
        public const string kHttpVerbPOST = "POST";

        public UnityWebRequest(string url, string method)
        {
            this.url = url;
        }

        public enum Result
        {
            InProgress,
            Success,
            ConnectionError,
            ProtocolError,
            DataProcessingError
        }

        public string url { get; set; }
        public int timeout { get; set; }
        public int redirectLimit { get; set; }
        public long responseCode { get; set; }
        public bool isNetworkError { get; set; }
        public Result result { get; set; }
        public DownloadHandler downloadHandler { get; set; }
        public UploadHandler uploadHandler { get; set; }

        public static UnityWebRequest Get(string url) => new UnityWebRequest(url, "GET");
        public void SetRequestHeader(string name, string value) { }
        public string GetResponseHeader(string name) => null;
        public UnityWebRequestAsyncOperation SendWebRequest() =>
            new UnityWebRequestAsyncOperation { webRequest = this };
        public void Abort() { }
        public void Dispose() { }
    }

    public sealed class UnityWebRequestAsyncOperation
    {
        public bool isDone { get; set; }
        public UnityWebRequest webRequest { get; set; }
    }
}
