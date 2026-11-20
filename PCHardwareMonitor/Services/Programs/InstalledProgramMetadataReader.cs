using System;
using System.IO;

namespace PCHardwareMonitor;

// Optional presentation metadata must never hide a valid uninstall registration.
internal static class InstalledProgramMetadataReader
{
    internal static T Read<T>(Func<T> read, T fallback, string identity,
        string stage, InstalledProgramsReadStatus? status)
    {
        try { return read(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException or
            System.ComponentModel.Win32Exception or NotSupportedException or
            InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            status?.MetadataWarnings.Add(new(identity, stage, ex.GetType().Name, ex.Message));
            return fallback;
        }
    }
}
