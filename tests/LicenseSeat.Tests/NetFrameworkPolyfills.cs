#if NETFRAMEWORK
// Compiler shim so the same test sources build for the .NET Framework 4.8 lane (#26).
// Test-only: the shipped library targets netstandard2.0 and needs none of this.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
#endif
