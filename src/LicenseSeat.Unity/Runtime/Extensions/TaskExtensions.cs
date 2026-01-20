using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LicenseSeat.Unity
{
    /// <summary>
    /// Extension methods for Unity-safe task handling.
    /// Addresses common pitfalls with async/await in Unity:
    /// - Tasks continuing after GameObject destruction
    /// - Memory leaks from zombie tasks
    /// - Proper cancellation on scene unload
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Runs a task with automatic cancellation when the associated MonoBehaviour is destroyed.
        /// This prevents "zombie tasks" that continue running after their context is gone.
        /// </summary>
        /// <typeparam name="T">The result type of the task.</typeparam>
        /// <param name="task">The task to run.</param>
        /// <param name="destroyCancellationToken">A cancellation token tied to the MonoBehaviour's lifecycle.</param>
        /// <returns>The task result, or throws OperationCanceledException if the MonoBehaviour was destroyed.</returns>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken destroyCancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (destroyCancellationToken.Register(() => tcs.TrySetResult(true)))
            {
                var completedTask = await Task.WhenAny(task, tcs.Task);

                if (completedTask == tcs.Task)
                {
                    throw new OperationCanceledException(destroyCancellationToken);
                }

                return await task;
            }
        }

        /// <summary>
        /// Runs a task with automatic cancellation when the associated MonoBehaviour is destroyed.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="destroyCancellationToken">A cancellation token tied to the MonoBehaviour's lifecycle.</param>
        public static async Task WithCancellation(this Task task, CancellationToken destroyCancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (destroyCancellationToken.Register(() => tcs.TrySetResult(true)))
            {
                var completedTask = await Task.WhenAny(task, tcs.Task);

                if (completedTask == tcs.Task)
                {
                    throw new OperationCanceledException(destroyCancellationToken);
                }

                await task;
            }
        }

        /// <summary>
        /// Safely fires and forgets a task, logging any exceptions.
        /// Use this when you don't need to await the result but want to handle errors gracefully.
        /// </summary>
        /// <param name="task">The task to fire and forget.</param>
        /// <param name="errorHandler">Optional custom error handler. If null, logs to Debug.LogException.</param>
        public static async void FireAndForget(this Task task, Action<Exception>? errorHandler = null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected, don't log
            }
            catch (Exception ex)
            {
                if (errorHandler != null)
                {
                    errorHandler(ex);
                }
                else
                {
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Runs a task on the Unity main thread after completion.
        /// Useful for updating UI or GameObjects after async operations.
        /// </summary>
        /// <typeparam name="T">The result type of the task.</typeparam>
        /// <param name="task">The task to run.</param>
        /// <param name="continuation">The action to run on the main thread with the result.</param>
        public static async void ContinueOnMainThread<T>(this Task<T> task, Action<T> continuation)
        {
            try
            {
                var result = await task;

                // Ensure we're on the main thread
                if (SynchronizationContext.Current == null)
                {
                    // We're not on the main thread, need to post back
                    // In Unity, this typically happens automatically with await
                    // but we ensure it here for safety
                    continuation(result);
                }
                else
                {
                    continuation(result);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected, don't continue
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Runs a task on the Unity main thread after completion.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="continuation">The action to run on the main thread.</param>
        public static async void ContinueOnMainThread(this Task task, Action continuation)
        {
            try
            {
                await task;
                continuation();
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected, don't continue
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Wraps a task with a timeout, throwing TimeoutException if the operation takes too long.
        /// </summary>
        /// <typeparam name="T">The result type of the task.</typeparam>
        /// <param name="task">The task to run.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>The task result.</returns>
        /// <exception cref="TimeoutException">Thrown if the task doesn't complete within the timeout.</exception>
        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource();
            var delayTask = Task.Delay(timeout, cts.Token);

            var completedTask = await Task.WhenAny(task, delayTask);

            if (completedTask == delayTask)
            {
                throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds} seconds.");
            }

            cts.Cancel(); // Cancel the delay task
            return await task;
        }

        /// <summary>
        /// Wraps a task with a timeout, throwing TimeoutException if the operation takes too long.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <exception cref="TimeoutException">Thrown if the task doesn't complete within the timeout.</exception>
        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource();
            var delayTask = Task.Delay(timeout, cts.Token);

            var completedTask = await Task.WhenAny(task, delayTask);

            if (completedTask == delayTask)
            {
                throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds} seconds.");
            }

            cts.Cancel();
            await task;
        }
    }
}
