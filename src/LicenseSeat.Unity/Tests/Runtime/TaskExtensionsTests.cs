#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LicenseSeat.Unity.Tests.Runtime
{
    /// <summary>
    /// Tests for Unity-safe task extensions.
    /// </summary>
    [TestFixture]
    public class TaskExtensionsTests
    {
        [Test]
        public async Task WithCancellation_CompletesNormally_WhenNotCancelled()
        {
            var cts = new CancellationTokenSource();
            var task = Task.FromResult(42);

            var result = await task.WithCancellation(cts.Token);

            Assert.That(result, Is.EqualTo(42));
        }

        [Test]
        public void WithCancellation_ThrowsOperationCancelled_WhenCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var task = Task.Delay(1000).ContinueWith(_ => 42);

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await task.WithCancellation(cts.Token);
            });
        }

        [Test]
        public async Task WithTimeout_CompletesNormally_WhenWithinTimeout()
        {
            var task = Task.FromResult(42);

            var result = await task.WithTimeout(TimeSpan.FromSeconds(5));

            Assert.That(result, Is.EqualTo(42));
        }

        [Test]
        public void WithTimeout_ThrowsTimeoutException_WhenExceedsTimeout()
        {
            var task = Task.Delay(5000).ContinueWith(_ => 42);

            Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await task.WithTimeout(TimeSpan.FromMilliseconds(10));
            });
        }

        [UnityTest]
        public IEnumerator FireAndForget_LogsException_WhenTaskFails()
        {
            var exceptionLogged = false;

            void LogHandler(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Exception && message.Contains("Test exception"))
                {
                    exceptionLogged = true;
                }
            }

            Application.logMessageReceived += LogHandler;

            try
            {
                var failingTask = Task.Run(() => throw new Exception("Test exception"));
                failingTask.FireAndForget();

                // Wait a bit for the exception to be logged
                yield return new WaitForSeconds(0.5f);

                Assert.That(exceptionLogged, Is.True);
            }
            finally
            {
                Application.logMessageReceived -= LogHandler;
            }
        }

        [Test]
        public void FireAndForget_DoesNotThrow_WhenTaskCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var task = Task.FromCanceled(cts.Token);

            // Should not throw
            Assert.DoesNotThrow(() => task.FireAndForget());
        }

        [Test]
        public async Task VoidTask_WithCancellation_CompletesNormally()
        {
            var cts = new CancellationTokenSource();
            var completed = false;
            var task = Task.Run(() => { completed = true; });

            await task.WithCancellation(cts.Token);

            Assert.That(completed, Is.True);
        }

        [Test]
        public async Task VoidTask_WithTimeout_CompletesNormally()
        {
            var completed = false;
            var task = Task.Run(() => { completed = true; });

            await task.WithTimeout(TimeSpan.FromSeconds(5));

            Assert.That(completed, Is.True);
        }
    }
}
#endif
