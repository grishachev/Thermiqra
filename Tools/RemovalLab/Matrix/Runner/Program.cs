using System.Text.Json;

namespace Thermiqra.RemovalLab;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static async Task<int> Main(string[] args)
    {
        bool retry = args.Length > 1 && args[0] == "--retry-reboot";
        bool resume = args.Length > 1 && (args[0] == "--resume" || retry);
        string output = Path.GetFullPath(args.Length > 1 && (args[0] == "--report" || resume) ? args[1] : Path.Combine(AppContext.BaseDirectory, "matrix-results.json"));
        MatrixReport report = new();
        bool saveReport = !resume;
        void Save() { Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllText(output, JsonSerializer.Serialize(report, Json)); }
        try
        {
            report.Environment = GuestSafety.Inspect();
            if (report.Environment.Length > 0) { report.Status = "Blocked"; Console.WriteLine(report.Environment); return 10; }
            if (resume)
            {
                var saved = JsonSerializer.Deserialize<MatrixReport>(File.ReadAllText(output), Json) ?? throw new IOException("Resume report missing");
                bool retryable = retry && saved.Status == "Failed" && saved.Cases.All(c =>
                    c.Id == "MsiReboot" ? c.Status == "Failed" : c.Status == "Passed");
                if ((!retryable && saved.Status != "AwaitingReboot") || !saved.Cases.Select(c => c.Id).SequenceEqual(report.Cases.Select(c => c.Id)))
                    throw new IOException("Unexpected resume state");
                if (retryable) File.Copy(output, output + ".before-retry.json", overwrite: false);
                report = saved;
                saveReport = true;
                var reboot = report.Cases.Single(c => c.Id == "MsiReboot");
                reboot.Detail = "";
                try { await MatrixPipeline.ResumeReboot(reboot); reboot.Status = "Passed"; }
                catch (Exception ex) { reboot.Status = "Failed"; reboot.Detail = ex.ToString(); }
                report.Status = report.Cases.All(c => c.Status == "Passed") ? "Passed" : "Failed";
                return report.Status == "Passed" ? 0 : 1;
            }
            report.Status = "Running"; Save();
            foreach (var scenario in report.Cases)
            {
                Console.WriteLine("Running " + scenario.Id);
                scenario.Status = "Running"; Save();
                try { await MatrixPipeline.Run(scenario); if (scenario.Status == "Running") scenario.Status = "Passed"; }
                catch (Exception ex) { scenario.Status = "Failed"; scenario.Detail = ex.ToString(); Console.WriteLine(ex.Message); }
                Save();
                if (scenario.Status == "AwaitingReboot") { report.Status = "AwaitingReboot"; return 20; }
            }
            report.Status = report.Cases.All(c => c.Status == "Passed") ? "Passed" : "Failed";
            return report.Status == "Passed" ? 0 : 1;
        }
        catch (Exception ex) { report.Status = "Failed"; report.Environment = ex.ToString(); return 1; }
        finally { report.FinishedUtc = report.Status == "AwaitingReboot" ? null : DateTime.UtcNow; if (saveReport) Save(); Console.WriteLine("Report: " + output + "; " + report.Status); }
    }
}
