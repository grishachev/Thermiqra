using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal sealed record InstalledProgramUninstallPlan(string Identity, string Name, string Version,
    string Kind, string Executable, string Arguments, bool Machine, string PackageFullName = "")
{
    internal string Fingerprint => string.Join("\u001f", Identity, Name, Version, Kind, Executable, Arguments, PackageFullName);
}

/// <summary>Always reread Windows registration; cached commands and directory guesses cannot launch removal.</summary>
internal static class InstalledProgramUninstallPlanReader
{
    internal static RegistryKey? OpenRegistration(string identity)
    {
        string[] parts = identity.Split(':', 4);
        if (parts.Length != 4 || parts[0] != "registry" ||
            (parts[1] != "HKLM" && parts[1] != "HKCU") ||
            (parts[2] != "64" && parts[2] != "32") || parts[3].Contains('\\'))
            throw new InvalidOperationException(SettingsService.L("Некорректная запись программы.", "Invalid application registration."));
        using RegistryKey root = RegistryKey.OpenBaseKey(parts[1] == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
            parts[2] == "64" ? RegistryView.Registry64 : RegistryView.Registry32);
        return root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + parts[3], false);
    }

    internal static async Task<InstalledProgramUninstallPlan> ReadAsync(InstalledProgramInfo app, bool quiet = false)
    {
        if (app.Kind == "MSIX")
        {
            string family = app.Identity.StartsWith("msix:", StringComparison.Ordinal) ? app.Identity[5..] : "";
            if (family.Length == 0) throw new InvalidOperationException(SettingsService.L(
                "Отсутствует идентификатор пакета.", "Package identity is missing."));
            string json = await InstalledProgramPackageOperations.QueryAsync("$p=" + InstalledProgramPackageOperations.FamilyQuery(family) +
                "; if($p.Count -ne 1){throw 'Package is missing or ambiguous'}; $p=$p[0]; [pscustomobject]@{Version=$p.Version.ToString();PackageFullName=$p.PackageFullName;NonRemovable=$p.NonRemovable;IsFramework=$p.IsFramework;IsResourcePackage=$p.IsResourcePackage;IsPartOfSystem=$p.IsPartOfSystem;SignatureKind=[string]$p.SignatureKind} | ConvertTo-Json -Compress");
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement p = document.RootElement;
            bool Flag(string key) => p.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
            if (Flag("NonRemovable") || Flag("IsFramework") || Flag("IsResourcePackage") || Flag("IsPartOfSystem") ||
                p.GetProperty("SignatureKind").GetString() == "System")
                throw new InvalidOperationException(SettingsService.L("Этот системный пакет нельзя удалить здесь.", "This system package cannot be removed here."));
            string fullName = p.GetProperty("PackageFullName").GetString()!;
            string version = p.GetProperty("Version").GetString()!;
            CheckVersion(app, version);
            return new(app.Identity, app.Name, version, "MSIX", "", "", false, fullName);
        }
        using RegistryKey entry = OpenRegistration(app.Identity) ?? throw new InvalidOperationException(
            SettingsService.L("Программа уже отсутствует. Обновите список.", "Application is already absent. Refresh the list."));
        string Read(string key) => Convert.ToString(entry.GetValue(key)) ?? "";
        if (Read("NoRemove") == "1" || Read("SystemComponent") == "1" || Read("ParentKeyName").Length > 0)
            throw new InvalidOperationException(SettingsService.L("Windows не разрешает отдельное удаление этой записи.", "Windows does not allow separate removal of this entry."));
        string name = Read("DisplayName").Trim();
        if (name != app.Name) throw new InvalidOperationException(SettingsService.L("Название записи изменилось. Обновите список.", "Registration name changed. Refresh the list."));
        string currentVersion = Read("DisplayVersion");
        CheckVersion(app, currentVersion);
        bool machine = app.Identity.StartsWith("registry:HKLM:", StringComparison.Ordinal);
        if (Read("WindowsInstaller") == "1")
        {
            if (!Guid.TryParse(app.Identity.Split(':', 4)[3], out Guid product)) throw new InvalidOperationException(SettingsService.L("Некорректный код MSI-продукта.", "Invalid MSI product code."));
            return new(app.Identity, name, currentVersion, "MSI",
                Path.Combine(Environment.SystemDirectory, "msiexec.exe"), $"/x {product:B} /norestart" + (quiet ? " /qn" : ""), machine);
        }
        string command = Environment.ExpandEnvironmentVariables(Read(quiet ? "QuietUninstallString" : "UninstallString").Trim());
        if (command.Length == 0 && quiet) command = Environment.ExpandEnvironmentVariables(Read("UninstallString").Trim());
        if (command.Length == 0) command = Environment.ExpandEnvironmentVariables(Read("QuietUninstallString").Trim());
        string executable;
        string arguments;
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end < 2) throw new InvalidOperationException(SettingsService.L("Некорректная команда удаления.", "Invalid uninstall command."));
            executable = command[1..end];
            arguments = command[(end + 1)..].TrimStart();
        }
        else
        {
            int end = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (end < 1) throw new InvalidOperationException(SettingsService.L("Нет поддерживаемой команды удаления.", "No supported uninstall command."));
            executable = command[..(end + 4)];
            arguments = command[(end + 4)..].TrimStart();
        }
        // No PATH search, cmd, scripting host, or automatic /I -> /X rewriting.
        if (!Path.IsPathFullyQualified(executable) && executable.Equals("rundll32.exe", StringComparison.OrdinalIgnoreCase))
            executable = Path.Combine(Environment.SystemDirectory, executable);
        string stem = Path.GetFileNameWithoutExtension(executable);
        if (!Path.IsPathFullyQualified(executable) || executable.StartsWith(@"\\", StringComparison.Ordinal) ||
            !Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            Array.Exists(new[] { "cmd", "powershell", "pwsh", "wscript", "cscript", "mshta", "msiexec" }, x => stem.Equals(x, StringComparison.OrdinalIgnoreCase)) ||
            !File.Exists(executable) || (File.GetAttributes(executable) & FileAttributes.ReparsePoint) != 0 ||
            InstalledProgramLocationService.HasReparseAncestor(Path.GetDirectoryName(executable)!))
            throw new InvalidOperationException(SettingsService.L("Штатный деинсталлятор не найден или команда не поддерживается.", "Native uninstaller was not found or its command is unsupported."));
        return new(app.Identity, name, currentVersion, "Win32", executable, arguments, machine);
    }

    private static void CheckVersion(InstalledProgramInfo app, string current)
    {
        if (current != app.Version) throw new InvalidOperationException(SettingsService.L(
            "Версия программы изменилась. Сначала обновите список.", "Application version changed. Refresh the list first."));
    }
}
