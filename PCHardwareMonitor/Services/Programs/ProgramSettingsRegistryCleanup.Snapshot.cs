using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal static partial class ProgramSettingsRegistryCleanup
{
    private static object Decode(ProgramRegistryText v) => (RegistryValueKind)v.Kind switch
    {
        RegistryValueKind.String or RegistryValueKind.ExpandString => v.Text,
        RegistryValueKind.Binary or RegistryValueKind.None => Convert.FromBase64String(v.Text),
        RegistryValueKind.DWord => int.Parse(v.Text, System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => long.Parse(v.Text, System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.MultiString => DecodeMultiString(v.Text),
        _ => throw new IOException(SettingsService.L("Неподдерживаемое значение реестра.", "Unsupported registry value."))
    };

    private static string[] DecodeMultiString(string text)
    {
        string[] values = JsonSerializer.Deserialize<string[]>(text) ?? throw new IOException(SettingsService.L("Некорректный массив данных реестра.", "Invalid registry array."));
        if (values.Any(value => value == null)) throw new IOException(SettingsService.L("Некорректный элемент массива данных реестра.", "Invalid registry array element."));
        return values;
    }

    private static List<ProgramRegistryText> Values(RegistryKey key)
    {
        if (key.ValueCount > 256) throw new IOException(SettingsService.L("Превышен предел числа значений реестра.", "Registry value limit exceeded."));
        List<ProgramRegistryText> values = new();
        foreach (string name in key.GetValueNames().OrderBy(x => x, StringComparer.Ordinal))
        {
            var kind = key.GetValueKind(name);
            object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            string text = value switch
            {
                string s => s, byte[] b => Convert.ToBase64String(b), string[] a => JsonSerializer.Serialize(a),
                int n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
                long n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => throw new IOException(SettingsService.L("Неподдерживаемые данные реестра.", "Unsupported registry data."))
            };
            if (name.Length > 16384 || text.Length > 65536) throw new IOException(SettingsService.L("Превышен предел объёма данных реестра.", "Registry data limit exceeded."));
            var stored = new ProgramRegistryText(name, text, (int)kind);
            Decode(stored); // Verify lossless type support before accepting a backup.
            values.Add(stored);
        }
        return values;
    }

    private static string Security(RegistryKey key)
    {
        int length = 0;
        ProgramLeftoverRegistryCleanup.RegGetKeySecurity(key.Handle, 7, null, ref length);
        if (length <= 0 || length > 65536) throw new IOException(SettingsService.L("Не удалось прочитать права доступа к реестру.", "Registry permissions unavailable."));
        byte[] data = new byte[length];
        int error = ProgramLeftoverRegistryCleanup.RegGetKeySecurity(key.Handle, 7, data, ref length);
        if (error != 0) throw new System.ComponentModel.Win32Exception(error);
        return Convert.ToBase64String(data);
    }

    internal static ProgramRegistrySnapshot Capture(ProgramLeftoverProbe p, bool requireMissing = true)
    {
        using RegistryKey key = Open(p, false);
        List<ProgramRegistryNode> nodes = new();
        Stopwatch budget = Stopwatch.StartNew();
        int bytes = 0, valueCount = 0;
        void Visit(RegistryKey current, string relative, int depth)
        {
            if (depth > 8 || nodes.Count >= 256 || budget.ElapsedMilliseconds > 2000)
                throw new IOException(SettingsService.L("Превышен предел числа объектов в снимке реестра.", "Registry snapshot limit exceeded."));
            var values = Values(current);
            string security = Security(current);
            bytes += security.Length + values.Sum(v => v.Name.Length + v.Text.Length);
            valueCount += values.Count;
            if (bytes > 400000 || valueCount > 2048) throw new IOException(SettingsService.L("Превышен предел размера снимка реестра.", "Registry snapshot size limit exceeded."));
            nodes.Add(new(relative, values, security));
            foreach (string child in current.GetSubKeyNames().OrderBy(x => x, StringComparer.Ordinal))
            {
                using RegistryKey nested = ProgramLeftoverRegistryCleanup.OpenWithoutLinks(current, child, View(p), false);
                Visit(nested, relative.Length == 0 ? child : relative + "\\" + child, depth + 1);
            }
        }
        Visit(key, "", 0);
        ProgramRegistrySnapshot snapshot = new(new(), nodes[0].Security, nodes);
        if (!Valid(snapshot)) throw new IOException(SettingsService.L("Снимок реестра не прошёл проверку.", "Registry snapshot validation failed."));
        CheckMachineReferences(p, snapshot, requireMissing);
        return snapshot;
    }

    internal static bool Valid(ProgramRegistrySnapshot s)
    {
        try
        {
            if (s.Tree == null || s.Tree.Count is < 1 or > 256 || s.Tree[0]?.Path != "" ||
                s.Values == null || s.Values.Count != 0 || s.Security != s.Tree[0].Security) return false;
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
            int size = 0, count = 0;
            foreach (var node in s.Tree)
            {
                if (node?.Path == null || node.Values == null || node.Security == null || !paths.Add(node.Path) ||
                    node.Path.Length > 2048 || (node.Path.Length > 0 && node.Path.Split('\\').Any(x => x.Length == 0 || x.Contains('\0') || x.Contains('/'))) ||
                    node.Path.Count(c => c == '\\') > 7 || node.Values.Count > 256) return false;
                int separator = node.Path.LastIndexOf('\\');
                if (node.Path.Length > 0 && !paths.Contains(separator < 0 ? "" : node.Path[..separator])) return false;
                byte[] security = Convert.FromBase64String(node.Security);
                if (security.Length is < 1 or > 65536) return false;
                _ = new System.Security.AccessControl.RawSecurityDescriptor(security, 0);
                HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
                size += node.Security.Length;
                foreach (var value in node.Values)
                {
                    if (value?.Name == null || value.Text == null || value.Name.Length > 16384 || value.Name.Contains('\0') ||
                        value.Name.Equals("SymbolicLinkValue", StringComparison.OrdinalIgnoreCase) || value.Text.Length > 65536 || !names.Add(value.Name)) return false;
                    Decode(value); size += value.Name.Length + value.Text.Length; count++;
                }
            }
            // Keep the escaped JSON payload below the backup store's limit.
            return size <= 400000 && count <= 2048 && JsonSerializer.SerializeToUtf8Bytes(s).Length <= 1024 * 1024;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or JsonException or IOException or ArgumentException) { return false; }
    }
}
