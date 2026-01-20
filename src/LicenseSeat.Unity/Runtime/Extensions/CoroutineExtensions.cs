#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace LicenseSeat
{
    /// <summary>
    /// Extension methods for converting Tasks to Coroutines in Unity.
    /// Provides Unity-friendly async patterns while avoiding common pitfalls.
    /// </summary>
    public static class CoroutineExtensions
    {
        /// <summary>
        /// Converts a Task to a Coroutine that can be started with StartCoroutine.
        /// </summary>
        /// <typeparam name="T">The task result type.</typeparam>
        /// <param name="task">The task to convert.</param>
        /// <param name="callback">Callback invoked when complete (result, error).</param>
        /// <returns>Coroutine enumerator.</returns>
        /// <example>
        /// <code>
        /// StartCoroutine(client.ActivateAsync("KEY").ToCoroutine((license, error) => {
        ///     if (error != null) Debug.LogError(error);
        ///     else Debug.Log($"Activated: {license.LicenseKey}");
        /// }));
        /// </code>
        /// </example>
        public static IEnumerator ToCoroutine<T>(this Task<T> task, Action<T, Exception> callback)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                callback?.Invoke(default, task.Exception?.InnerException ?? task.Exception);
            }
            else if (task.IsCanceled)
            {
                callback?.Invoke(default, new OperationCanceledException());
            }
            else
            {
                callback?.Invoke(task.Result, null);
            }
        }

        /// <summary>
        /// Converts a Task to a Coroutine that can be started with StartCoroutine.
        /// </summary>
        /// <param name="task">The task to convert.</param>
        /// <param name="callback">Callback invoked when complete (error or null if success).</param>
        /// <returns>Coroutine enumerator.</returns>
        /// <example>
        /// <code>
        /// StartCoroutine(client.DeactivateAsync().ToCoroutine(error => {
        ///     if (error != null) Debug.LogError(error);
        ///     else Debug.Log("Deactivated successfully");
        /// }));
        /// </code>
        /// </example>
        public static IEnumerator ToCoroutine(this Task task, Action<Exception> callback)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                callback?.Invoke(task.Exception?.InnerException ?? task.Exception);
            }
            else if (task.IsCanceled)
            {
                callback?.Invoke(new OperationCanceledException());
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        /// <summary>
        /// Starts a Task-returning method as a fire-and-forget coroutine on a MonoBehaviour.
        /// Logs any errors to the Unity console.
        /// </summary>
        /// <typeparam name="T">The task result type.</typeparam>
        /// <param name="mono">The MonoBehaviour to run the coroutine on.</param>
        /// <param name="taskFunc">Function that returns the task.</param>
        /// <param name="onComplete">Optional callback on success.</param>
        /// <param name="onError">Optional callback on error.</param>
        /// <returns>The started Coroutine.</returns>
        /// <example>
        /// <code>
        /// this.RunTaskAsCoroutine(
        ///     () => client.ActivateAsync("KEY"),
        ///     license => Debug.Log($"Activated: {license.LicenseKey}"),
        ///     error => ShowErrorUI(error.Message)
        /// );
        /// </code>
        /// </example>
        public static Coroutine RunTaskAsCoroutine<T>(
            this MonoBehaviour mono,
            Func<Task<T>> taskFunc,
            Action<T> onComplete = null,
            Action<Exception> onError = null)
        {
            if (mono == null)
            {
                throw new ArgumentNullException(nameof(mono));
            }

            if (taskFunc == null)
            {
                throw new ArgumentNullException(nameof(taskFunc));
            }

            return mono.StartCoroutine(RunTaskCoroutine(taskFunc, onComplete, onError));
        }

        /// <summary>
        /// Starts a Task-returning method as a fire-and-forget coroutine on a MonoBehaviour.
        /// Logs any errors to the Unity console.
        /// </summary>
        /// <param name="mono">The MonoBehaviour to run the coroutine on.</param>
        /// <param name="taskFunc">Function that returns the task.</param>
        /// <param name="onComplete">Optional callback on success.</param>
        /// <param name="onError">Optional callback on error.</param>
        /// <returns>The started Coroutine.</returns>
        public static Coroutine RunTaskAsCoroutine(
            this MonoBehaviour mono,
            Func<Task> taskFunc,
            Action onComplete = null,
            Action<Exception> onError = null)
        {
            if (mono == null)
            {
                throw new ArgumentNullException(nameof(mono));
            }

            if (taskFunc == null)
            {
                throw new ArgumentNullException(nameof(taskFunc));
            }

            return mono.StartCoroutine(RunTaskCoroutine(taskFunc, onComplete, onError));
        }

        private static IEnumerator RunTaskCoroutine<T>(
            Func<Task<T>> taskFunc,
            Action<T> onComplete,
            Action<Exception> onError)
        {
            Task<T> task;
            try
            {
                task = taskFunc();
            }
            catch (Exception ex)
            {
                if (onError != null)
                {
                    onError(ex);
                }
                else
                {
                    Debug.LogException(ex);
                }
                yield break;
            }

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                var ex = task.Exception?.InnerException ?? task.Exception;
                if (onError != null)
                {
                    onError(ex);
                }
                else
                {
                    Debug.LogException(ex);
                }
            }
            else if (task.IsCanceled)
            {
                var ex = new OperationCanceledException();
                if (onError != null)
                {
                    onError(ex);
                }
                else
                {
                    Debug.LogWarning("[LicenseSeat SDK] Task was canceled");
                }
            }
            else
            {
                onComplete?.Invoke(task.Result);
            }
        }

        private static IEnumerator RunTaskCoroutine(
            Func<Task> taskFunc,
            Action onComplete,
            Action<Exception> onError)
        {
            Task task;
            try
            {
                task = taskFunc();
            }
            catch (Exception ex)
            {
                if (onError != null)
                {
                    onError(ex);
                }
                else
                {
                    Debug.LogException(ex);
                }
                yield break;
            }

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                var ex = task.Exception?.InnerException ?? task.Exception;
                if (onError != null)
                {
                    onError(ex);
                }
                else
                {
                    Debug.LogException(ex);
                }
            }
            else if (task.IsCanceled)
            {
                var ex = new OperationCanceledException();
                if (onError != null)
                {
                    onError(ex);
                }
                else
                {
                    Debug.LogWarning("[LicenseSeat SDK] Task was canceled");
                }
            }
            else
            {
                onComplete?.Invoke();
            }
        }
    }
}
#endif
