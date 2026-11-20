using System;
using System.Collections.Concurrent;
using System.IO;

namespace PCHardwareMonitor;

// The observer still records catalog changes, but must not replace an
// in-flight uninstall's pre-removal snapshot with a half-removed installation.
internal sealed class ProgramNativeRemovalScope : IDisposable
{
    private static readonly ConcurrentDictionary<string, byte> Active = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _identity;

    internal ProgramNativeRemovalScope(string identity)
    {
        _identity = identity;
        if (!Active.TryAdd(identity, 0)) throw new IOException(SettingsService.L(
            "Удаление этой программы уже выполняется.", "This application is already being uninstalled."));
    }

    internal static bool Contains(string identity) => Active.ContainsKey(identity);
    public void Dispose() => Active.TryRemove(_identity, out _);
}
