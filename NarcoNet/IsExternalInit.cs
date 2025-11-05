// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace must match folder structure - this is a compiler polyfill
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130

/// <summary>
///     Polyfill for init-only setters in .NET Framework 4.7.2
///     This allows using C# 9.0 record types on older frameworks
/// </summary>
internal static class IsExternalInit
{
}
