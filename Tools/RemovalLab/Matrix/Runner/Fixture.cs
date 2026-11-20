using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace Thermiqra.RemovalLab;

internal sealed record FixtureFile(string Path, string Hash, bool Preserve);
internal sealed record Fixture(int Schema, string Id, string Name, string RunId, List<string> Roots,
    List<FixtureFile> Files, string RegistryPath, string NeighborKey)
{
    internal string Control => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThermiqraMatrixControl", Name);
    internal static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    internal static async Task<Fixture> Install(string id)
    {
        if (id.StartsWith("Msi"))
        {
            string targetUser = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
            int code = await RunProcess(Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                "/i \"" + Path.Combine(AppContext.BaseDirectory, "Packages", id + ".msi") + "\" /qn /norestart /l*v \"" + Path.Combine(Path.GetTempPath(), "ThermiqraMatrix-" + id + "-install.log") + "\"" +
                " LAB_LOCAL=\"" + Local + "\" LAB_ROAMING=\"" + Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\" LAB_SID=\"" + targetUser + "\"");
            if (code != 0) throw new IOException("MSI install failed: " + code);
        }
        else if (await RunProcess(Path.Combine(AppContext.BaseDirectory, "MatrixSubject.exe"), "--install " + id) != 0)
            throw new IOException("Fixture installation failed.");
        return Load(id);
    }

    internal static Fixture Load(string id)
    {
        string name = "ThermiqraMatrix" + id;
        string control = Path.Combine(Local, "ThermiqraMatrixControl", name);
        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(Path.Combine(control, "manifest.json")), Program.Json) ?? throw new IOException("Manifest missing.");
        string root = Path.Combine(id.StartsWith("Msi") && id != "MsiUser" ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) : Path.Combine(Local, "Programs"), name);
        string[] roots = { root, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), name), Path.Combine(Local, name) };
        if (fixture.Schema != 1 || fixture.Name != name || fixture.Id != id || !fixture.Roots.SequenceEqual(roots) ||
            fixture.RegistryPath != @"Software\ThermiqraLab\" + name || fixture.NeighborKey != @"Software\ThermiqraLab\Neighbor" + name ||
            fixture.Files.Count != 9 || !Guid.TryParseExact(fixture.RunId, "N", out _)) throw new IOException("Manifest scope mismatch.");
        foreach (var file in fixture.Files)
        {
            bool allowed = file.Preserve ? file.Path == Path.Combine(Local, "ThermiqraMatrixNeighbors", name, "keep.txt") :
                roots.Any(r => PCHardwareMonitor.WindowsPathRules.Inside(file.Path, r) && file.Path != r);
            // A resumed cleanup legitimately loads paths that no longer exist.
            // The production reparse guard treats missing paths as unverified;
            // that is correct for mutation, but not for this read-only manifest.
            if (!allowed || ((File.Exists(file.Path) || Directory.Exists(file.Path)) &&
                PCHardwareMonitor.InstalledProgramLocationService.HasReparseAncestor(file.Path))) throw new IOException("Manifest file scope mismatch.");
        }
        return fixture;
    }

    internal static async Task<int> RunProcess(string executable, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true }) ?? throw new IOException("Process did not start.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        await process.WaitForExitAsync(timeout.Token); // Never kill a native installer midway through work.
        return process.ExitCode;
    }
    internal static string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }
    internal void VerifyNeighbors(CaseResult result)
    {
        foreach (var file in Files.Where(f => f.Preserve)) result.Require(File.Exists(file.Path) && Hash(file.Path) == file.Hash, "Neighbor file unchanged");
        using var neighbor = Registry.CurrentUser.OpenSubKey(NeighborKey);
        result.Require(neighbor?.GetValue("RunId") as string == RunId, "Neighbor registry key unchanged");
    }
    internal void VerifyGone(CaseResult result)
    {
        foreach (string root in Roots) result.Require(!Directory.Exists(root) && !File.Exists(root), "Fixture directory absent: " + root);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
            using var key = user.OpenSubKey(RegistryPath);
            result.Require(key == null, "Fixture registry key absent: " + view);
        }
        if (Id.StartsWith("Msi"))
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var record = registry.OpenSubKey(@"Software\ThermiqraMatrixInstallRecords\" + Name);
                result.Require(record == null, "MSI-owned installation marker absent: " + hive + " " + view);
            }
        else
        {
            using var registration = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Name);
            result.Require(registration == null, "Native uninstall registration absent");
        }
        VerifyNeighbors(result);
    }
    internal void VerifyRegistry(CaseResult result)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        result.Require(key?.GetValue("RunId") as string == RunId && key.GetValueKind("Number") == RegistryValueKind.DWord &&
            (int)key.GetValue("Number")! == 42 && key.GetValueKind("Expanded") == RegistryValueKind.ExpandString &&
            (string)key.GetValue("Expanded", "", RegistryValueOptions.DoNotExpandEnvironmentNames)! == "%LOCALAPPDATA%\\" + Name &&
            ((byte[])key.GetValue("Binary")!).SequenceEqual(new byte[] { 0, 127, 255 }) &&
            ((string[])key.GetValue("List")!).SequenceEqual(new[] { "first", "second" }), "Restored registry types and contents match the independent manifest");
    }
}
