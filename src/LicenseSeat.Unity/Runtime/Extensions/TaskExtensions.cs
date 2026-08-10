#nullable enable
#if UNITY_5_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LicenseSeat.Unity
{
    /// <summary>
    /// Task helpers with explicit cancellation, timeout, observation, and Unity
    /// synchronization-context behavior. None of these helpers use async void.
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Stops awaiting a task when the supplied lifetime token is canceled.
        /// This does not cancel the underlying operation; pass the same token to
        /// that operation when cooperative cancellation is required.
        /// </summary>
        public static async Task<T> WithCancellation<T>(
            this Task<T> task,
            CancellationToken lifetimeToken)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            lifetimeToken.ThrowIfCancellationRequested();
            var cancellation = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using (lifetimeToken.Register(() => cancellation.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancellation.Task).ConfigureAwait(false) == cancellation.Task)
                {
                    throw new OperationCanceledException(lifetimeToken);
                }

                return await task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Stops awaiting a task when the supplied lifetime token is canceled.
        /// </summary>
        public static async Task WithCancellation(
            this Task task,
            CancellationToken lifetimeToken)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            lifetimeToken.ThrowIfCancellationRequested();
            var cancellation = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using (lifetimeToken.Register(() => cancellation.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancellation.Task).ConfigureAwait(false) == cancellation.Task)
                {
                    throw new OperationCanceledException(lifetimeToken);
                }

                await task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Observes a task and handles its exception. Callers should retain or
        /// explicitly discard the returned Task so the lifetime choice is visible.
        /// </summary>
        public static async Task FireAndForget(
            this Task task,
            Action<Exception>? errorHandler = null)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected for lifetime-bound work.
            }
            catch (Exception ex)
            {
                if (errorHandler != null)
                {
                    errorHandler(ex);
                }
                else
                {
                    Debug.LogError("[LicenseSeat SDK] An observed background task failed.");
                }
            }
        }

        /// <summary>
        /// Runs a continuation on the Unity synchronization context captured at
        /// invocation. Invoke this method from Unity's main thread.
        /// </summary>
        public static async Task ContinueOnMainThread<T>(
            this Task<T> task,
            Action<T> continuation)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            var unityContext = SynchronizationContext.Current ??
                throw new InvalidOperationException(
                    "ContinueOnMainThread must be invoked from Unity's main thread.");

            var result = await task.ConfigureAwait(false);
            await PostAsync(unityContext, () => continuation(result)).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs a continuation on the Unity synchronization context captured at
        /// invocation. Invoke this method from Unity's main thread.
        /// </summary>
        public static async Task ContinueOnMainThread(
            this Task task,
            Action continuation)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            var unityContext = SynchronizationContext.Current ??
                throw new InvalidOperationException(
                    "ContinueOnMainThread must be invoked from Unity's main thread.");

            await task.ConfigureAwait(false);
            await PostAsync(unityContext, continuation).ConfigureAwait(false);
        }

        /// <summary>
        /// Stops awaiting a task after a timeout. This does not cancel the
        /// underlying operation; pass a cancellation token to that operation too.
        /// </summary>
        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            ValidateTimeout(timeout);
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                return await task.ConfigureAwait(false);
            }

            using var timeoutCancellation = new CancellationTokenSource();
            var delay = Task.Delay(timeout, timeoutCancellation.Token);
            if (await Task.WhenAny(task, delay).ConfigureAwait(false) == delay)
            {
                _ = ObserveLateCompletionAsync(task);
                throw new TimeoutException("Operation timed out.");
            }

            timeoutCancellation.Cancel();
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Stops awaiting a task after a timeout. This does not cancel the
        /// underlying operation; pass a cancellation token to that operation too.
        /// </summary>
        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            ValidateTimeout(timeout);
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                await task.ConfigureAwait(false);
                return;
            }

            using var timeoutCancellation = new CancellationTokenSource();
            var delay = Task.Delay(timeout, timeoutCancellation.Token);
            if (await Task.WhenAny(task, delay).ConfigureAwait(false) == delay)
            {
                _ = ObserveLateCompletionAsync(task);
                throw new TimeoutException("Operation timed out.");
            }

            timeoutCancellation.Cancel();
            await task.ConfigureAwait(false);
        }

        private static void ValidateTimeout(TimeSpan timeout)
        {
            if (timeout != Timeout.InfiniteTimeSpan && timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive or infinite.");
            }
        }

        private static async Task ObserveLateCompletionAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Deliberately observe a fault after the caller has timed out.
            }
        }

        private static Task PostAsync(SynchronizationContext context, Action continuation)
        {
            if (ReferenceEquals(SynchronizationContext.Current, context))
            {
                continuation();
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            context.Post(
                _ =>
                {
                    try
                    {
                        continuation();
                        completion.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                },
                null);
            return completion.Task;
        }
    }
}
#endif
