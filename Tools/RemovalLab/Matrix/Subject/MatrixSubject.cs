// Test payload, never part of Thermiqra. All writes require a disposable guest.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

internal static class MatrixSubject
{
    private static readonly string[] Ids = { "Normal", "PartialError", "Error", "Cancel", "Helper", "Locked", "Denied", "Shared", "Reparse", "Restore", "Collision", "MsiUser", "MsiMachine", "MsiCancel", "MsiError", "MsiReboot" };
    private static string localOverride, roamingOverride, userSid;
    private static string Local { get { return localOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
    private static string Roaming { get { return roamingOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }
    private static RegistryKey User()
    {
        if (string.IsNullOrEmpty(userSid)) return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        RegistryKey key = Registry.Users.OpenSubKey(userSid, true);
        if (key == null) throw new IOException("Target test user is not loaded.");
        return key;
    }
    private static string Name(string id) { if (!Ids.Contains(id)) throw new ArgumentException("Unknown fixture"); return "ThermiqraMatrix" + id; }
    private static string Root(string id) { return Path.Combine(id.StartsWith("Msi") && id != "MsiUser" ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) : Path.Combine(Local, "Programs"), Name(id)); }
    private static string Control(string id) { return Path.Combine(Local, "ThermiqraMatrixControl", Name(id)); }

    private static void EnsureGuest()
    {
        string marker = @"C:\ThermiqraDisposableLab\guest-enabled.marker";
        bool guestMarker = File.Exists(marker) && File.ReadAllText(marker) == "thermiqra-disposable-guest-v1";
        if (Environment.GetEnvironmentVariable("THERMIQRA_DISPOSABLE_LAB") != "1" && !guestMarker) throw new InvalidOperationException("Disposable-guest flag missing.");
        bool guest = false;
        using (var query = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem"))
        using (var rows = query.Get())
            foreach (ManagementObject row in rows) using (row)
            {
                string model = Convert.ToString(row["Model"]).ToLowerInvariant();
                guest |= new[] { "virtual machine", "virtualbox", "vmware", "kvm", "qemu", "parallels" }.Any(model.Contains);
            }
        if (!guest) throw new InvalidOperationException("This is not a recognized disposable guest.");
    }

    private static int Main(string[] args)
    {
        try
        {
            EnsureGuest();
            if (args.Length < 2) throw new ArgumentException("Expected operation and fixture id");
            string id = args[1]; Name(id);
            if (args[0] == "--install") { Install(id); return 0; }
            if (args[0] == "--seed")
            {
                if (args.Length == 5)
                {
                    localOverride = Path.GetFullPath(args[2]); roamingOverride = Path.GetFullPath(args[3]);
                    userSid = new System.Security.Principal.SecurityIdentifier(args[4]).Value;
                }
                Seed(id); return 0;
            }
            if (args[0] == "--uninstall")
            {
                if (id == "Cancel") return 0; // Registration remains: completion must not be claimed.
                if (id == "Error") return 5;
                string worker = Path.Combine(Path.GetTempPath(), "ThermiqraMatrixWorkers", Name(id) + ".exe");
                Directory.CreateDirectory(Path.GetDirectoryName(worker));
                File.Copy(typeof(MatrixSubject).Assembly.Location, worker, false);
                Process.Start(new ProcessStartInfo(worker, "--worker " + id + " " + Process.GetCurrentProcess().Id) { UseShellExecute = false, CreateNoWindow = true });
                return id == "PartialError" ? 5 : 0;
            }
            if (args[0] == "--worker")
            {
                try { using (var parent = Process.GetProcessById(int.Parse(args[2]))) parent.WaitForExit(); } catch (ArgumentException) { }
                Thread.Sleep(300);
                File.Delete(Path.Combine(Root(id), "MatrixSubject.exe"));
                using (var user = User()) user.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Name(id), false);
                if (id == "Helper")
                {
                    File.WriteAllText(Path.Combine(Control(id), "helper.ready"), "running");
                    var timer = Stopwatch.StartNew();
                    while (timer.Elapsed < TimeSpan.FromSeconds(90) && !File.Exists(Path.Combine(Control(id), "release"))) Thread.Sleep(100);
                }
                return 0;
            }
            if (args[0] == "--hold")
            {
                using (var held = new FileStream(Path.Combine(Root(id), "Runtime", "locked.dat"), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    File.WriteAllText(Path.Combine(Control(id), "lock.ready"), "locked");
                    var timer = Stopwatch.StartNew();
                    while (timer.Elapsed < TimeSpan.FromMinutes(4) && !File.Exists(Path.Combine(Control(id), "release"))) Thread.Sleep(100);
                }
                return 0;
            }
            throw new ArgumentException("Unknown operation");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 10; }
    }

    private static void Install(string id)
    {
        if (Directory.Exists(Root(id)) || Directory.Exists(Control(id))) throw new IOException("Fixture already exists.");
        Directory.CreateDirectory(Root(id));
        string exe = Path.Combine(Root(id), "MatrixSubject.exe");
        File.Copy(typeof(MatrixSubject).Assembly.Location, exe);
        using (var user = User())
        using (var registration = user.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Name(id)))
        {
            registration.SetValue("DisplayName", Name(id)); registration.SetValue("DisplayVersion", "1.0");
            registration.SetValue("Publisher", "ThermiqraLab"); registration.SetValue("InstallLocation", Root(id));
            registration.SetValue("DisplayIcon", exe); registration.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            string command = "\"" + exe + "\" --uninstall " + id;
            registration.SetValue("UninstallString", command); registration.SetValue("QuietUninstallString", command);
            registration.Flush();
        }
        Seed(id);
    }

    private static void Seed(string id)
    {
        string root = Root(id), name = Name(id), control = Control(id);
        if (!Directory.Exists(root) || Directory.Exists(control)) throw new IOException("Unexpected fixture state.");
        string roaming = Path.Combine(Roaming, name);
        string local = Path.Combine(Local, name);
        string neighbor = Path.Combine(Local, "ThermiqraMatrixNeighbors", name);
        foreach (string directory in new[] { roaming, local, neighbor })
            if (Directory.Exists(directory)) throw new IOException("Fixture data already exists.");
        Directory.CreateDirectory(control);
        string run = Guid.NewGuid().ToString("N");
        List<object> files = new List<object>();
        Action<string, bool> create = (path, preserve) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { byte[] bytes = Encoding.UTF8.GetBytes("Known fixture " + run + " " + path); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            string hash;
            using (var sha = SHA256.Create()) using (var input = File.OpenRead(path)) hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
            files.Add(new { Path = path, Hash = hash, Preserve = preserve });
        };
        create(Path.Combine(root, "Runtime", "locked.dat"), false);
        create(Path.Combine(root, "Settings", "settings.json"), false);
        for (int i = 0; i < 4; i++) create(Path.Combine(root, "Cache", "sample" + i + ".dat"), false);
        create(Path.Combine(roaming, "Profiles", "preferences.json"), false);
        create(Path.Combine(local, "Cache", "cache.bin"), false);
        create(Path.Combine(neighbor, "keep.txt"), true);
        using (var user = User())
        using (var key = user.CreateSubKey(@"Software\ThermiqraLab\" + name))
        {
            key.SetValue("RunId", run); key.SetValue("Number", 42, RegistryValueKind.DWord);
            key.SetValue("Binary", new byte[] { 0, 127, 255 }, RegistryValueKind.Binary);
            key.SetValue("List", new[] { "first", "second" }, RegistryValueKind.MultiString);
            key.SetValue("Expanded", "%LOCALAPPDATA%\\" + name, RegistryValueKind.ExpandString); key.Flush();
        }
        using (var user = User())
        using (var key = user.CreateSubKey(@"Software\ThermiqraLab\Neighbor" + name)) { key.SetValue("RunId", run); key.Flush(); }
        File.WriteAllText(Path.Combine(control, "manifest.json"), new JavaScriptSerializer().Serialize(new
        {
            Schema = 1, Id = id, Name = name, RunId = run, Roots = new[] { root, roaming, local }, Files = files,
            RegistryPath = @"Software\ThermiqraLab\" + name, NeighborKey = @"Software\ThermiqraLab\Neighbor" + name
        }), Encoding.UTF8);
    }
}
