#if NETFRAMEWORK
// BCL members the tests use that .NET Framework 4.8 lacks (#26). Test-only.
namespace LicenseSeat.Tests;

internal static class NetFrameworkExtensions
{
    internal static string Replace(this string value, string oldValue, string newValue, StringComparison comparison)
    {
        if (comparison != StringComparison.Ordinal)
        {
            throw new NotSupportedException("Only ordinal replacement is polyfilled.");
        }

        return value.Replace(oldValue, newValue);
    }

    internal static bool Contains(this string value, string substring, StringComparison comparison)
    {
        return value.IndexOf(substring, comparison) >= 0;
    }

    internal static async Task WaitAsync(this Task task, TimeSpan timeout)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false) != task)
        {
            throw new TimeoutException();
        }

        await task.ConfigureAwait(false);
    }

    internal static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false) != task)
        {
            throw new TimeoutException();
        }

        return await task.ConfigureAwait(false);
    }
}
#endif
