// Deliberately targets the Windows .NET Framework compiler: no runtime download.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class RemovalLab
{
    private const string Product = "ThermiqraRemovalLab";
    private const string RegistryPath = @"Software\ThermiqraLab\ThermiqraRemovalLab";
    private const string NeighborKey = @"Software\ThermiqraLab\Neighbor";
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string Control = Path.Combine(Local, "ThermiqraLabVerification");

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--seed") { Seed(); return 0; }
            Application.EnableVisualStyles();
            Form form = new Form { Text = "Thermiqra Removal Lab — тестовое приложение", Width = 620, Height = 270 };
            form.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(20), AutoSize = false,
                Text = "Это специальное приложение для проверки удаления.\r\n\r\n" +
                    "Оно создаёт известные папки, настройки и соседние контрольные данные.\r\n" +
                    "Удалите «Thermiqra Removal Lab» через Thermiqra, затем выполните Verify-Lab.ps1.\r\n\r\n" +
                    "Образец рассчитан на отдельную виртуальную Windows.\r\n" +
                    "Службы, драйверы, автозапуск и сетевые настройки не устанавливаются." });
            Application.Run(form); return 0;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Thermiqra Removal Lab", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var input = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
    }

    private static void Seed()
    {
        string install = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        string expected = Path.Combine(Local, "Programs", Product);
        if (!install.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unexpected test installation directory.");
        string roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Product);
        string local = Path.Combine(Local, Product);
        string neighbor = Path.Combine(Local, "ThermiqraLabNeighbor");
        foreach (string directory in new[] { roaming, local, neighbor, Control })
            if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Test path already occupied: " + directory);
        using (var own = Registry.CurrentUser.OpenSubKey(RegistryPath))
        using (var other = Registry.CurrentUser.OpenSubKey(NeighborKey))
            if (own != null || other != null) throw new IOException("Test registry namespace already occupied.");

        string run = Guid.NewGuid().ToString("N");
        List<object> files = new List<object>();
        Action<string, string, bool> create = (path, content, preserve) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { byte[] bytes = Encoding.UTF8.GetBytes(content); output.Write(bytes, 0, bytes.Length); output.Flush(true); }
            files.Add(new { Path = path, Sha256 = Hash(path), Preserve = preserve });
        };
        create(Path.Combine(install, "Runtime", "settings.json"), "{\"test\":\"" + run + "\"}", false);
        create(Path.Combine(install, "Plugins", "Example", "plugin.data"), "Plugin data " + run, false);
        for (int i = 0; i < 60; i++)
            create(Path.Combine(install, "Cache", "bucket" + (i % 3), "sample" + i.ToString("D3") + ".dat"), "Known test leftover " + run + " " + i, false);
        create(Path.Combine(roaming, "Profiles", "Default", "preferences.json"), "{\"run\":\"" + run + "\"}", false);
        create(Path.Combine(local, "Logs", "session.log"), "Test log " + run, false);
        create(Path.Combine(local, "Cache", "state.bin"), "Cache " + run, false);
        create(Path.Combine(neighbor, "keep.txt"), "Neighbor must remain " + run, true);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view))
        using (var key = user.CreateSubKey(RegistryPath))
        {
            key.SetValue("RunId", run, RegistryValueKind.String);
            key.SetValue("InstallPath", install, RegistryValueKind.String);
            key.SetValue("Number", 42, RegistryValueKind.DWord);
            key.SetValue("Items", new[] { "first", "second" }, RegistryValueKind.MultiString);
            key.SetValue("Bytes", new byte[] { 0, 17, 255 }, RegistryValueKind.Binary); key.Flush();
        }
        using (var key = Registry.CurrentUser.CreateSubKey(NeighborKey))
        { key.SetValue("RunId", run, RegistryValueKind.String); key.Flush(); }
        Directory.CreateDirectory(Control);
        var manifest = new { Schema = 1, RunId = run, CreatedUtc = DateTime.UtcNow.ToString("O"), InstallRoot = install,
            CleanupRoots = new[] { install, roaming, local }, Files = files, RegistryKey = RegistryPath,
            NeighborKey = NeighborKey, UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ThermiqraRemovalLab_is1" };
        File.WriteAllText(Path.Combine(Control, "manifest.json"), new JavaScriptSerializer().Serialize(manifest), Encoding.UTF8);
    }
}
