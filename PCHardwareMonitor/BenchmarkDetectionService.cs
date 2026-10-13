using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PCHardwareMonitor;

[Flags]
public enum BenchmarkWorkloadKind
{
    None = 0,
    Cpu = 1,
    Gpu = 2,
    Memory = 4,
    Storage = 8
}

internal static class BenchmarkDetectionService
{
    private const float CpuActiveThreshold = 65.0f;
    private const float GpuActiveThreshold = 60.0f;
    private const float GpuMemoryActiveThreshold = 60.0f;
    private const float MemoryActiveThreshold = 80.0f;

    private static readonly BenchmarkProcessRule[] DedicatedRules =
    {
        // CPU / CPU + memory
        new(new[] { "cinebench" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu),
        new(new[] { "prime95", "mprime" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Memory),
        new(new[] { "ycruncher" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Memory),
        new(new[] { "linx", "linpack", "intelburntest" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Memory),
        new(new[] { "wprime", "superpi", "hyperpi", "pifast" }, BenchmarkWorkloadKind.Cpu),
        new(new[] { "cpustres", "cpuburn", "cpuburnin" }, BenchmarkWorkloadKind.Cpu),
        new(new[] { "corecycler" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Memory),
        new(new[] { "powermax" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu),
        new(new[] { "coronabenchmark" }, BenchmarkWorkloadKind.Cpu),

        // GPU / rendering
        new(new[] { "furmark", "furmark2" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "msikombustor", "kombustor" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "heaven", "unigineheaven" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "valley", "uniginevalley" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "superposition", "uniginesuperposition" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "gputest", "gpupi" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "octanebench" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "redshiftbenchmark" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "basemarkgpu" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "gfxbench" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "catzilla" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "ezbench" }, BenchmarkWorkloadKind.Gpu),
        new(new[] { "specviewperf", "viewperf" }, BenchmarkWorkloadKind.Gpu),

        // Benchmarks that can exercise CPU and/or GPU
        new(new[] { "3dmark" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu),
        new(new[] { "geekbench", "geekbench5", "geekbench6", "geekbenchai" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu),
        new(new[] { "vraybenchmark", "vraybench" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu),
        new(new[] { "blenderbenchmark", "benchmarklauncher" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu),
        new(new[] { "luxmark" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu),
        new(new[] { "pugetbench" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu),

        // Memory
        new(new[] { "testmem5", "tm5" }, BenchmarkWorkloadKind.Memory),
        new(new[] { "memtest64", "memtestpro", "memtest" }, BenchmarkWorkloadKind.Memory),
        new(new[] { "ramtest", "karhuramtest", "karhu" }, BenchmarkWorkloadKind.Memory),
        new(new[] { "memtestvulkan", "gpumemtest" }, BenchmarkWorkloadKind.Gpu),

        // Storage
        new(new[] { "crystaldiskmark", "diskmark32", "diskmark64", "diskmark" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "diskspd" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "asssdbenchmark", "asssd" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "attodiskbenchmark", "attobenchmark" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "iometer" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "hdtune", "hdtunepro" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "txbench" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "anvilstorageutilities", "anvilpro" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "parkdale" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "blackmagicdiskspeedtest" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "diskbench" }, BenchmarkWorkloadKind.Storage),
        new(new[] { "fio" }, BenchmarkWorkloadKind.Storage),

        // Combined benchmark suites
        new(new[] { "pcmark" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "performancetest", "ptx64", "ptx32" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "novabench" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "userbenchmark" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "crossmark" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "sysmark" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "mobilemark" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "specworkstation" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "winsat" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Storage),
        new(new[] { "procyonbenchmark", "ulprocyon" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "burnintest" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "heavyload" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage),
        new(new[] { "stressmypc" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Storage),
        new(new[] { "sisoftsandra", "sandralite", "sandraplatinum" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory | BenchmarkWorkloadKind.Storage)
    };

    private static readonly BenchmarkProcessRule[] ActiveLoadRules =
    {
        // OCCT is a test suite with different modes. The active component is
        // inferred from the hardware load at the same sampling moment.
        new(new[] { "occt", "occtpersonal", "occtenterprise" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory)
    };

    private static readonly AmbiguousBenchmarkRule[] AmbiguousRules =
    {
        // These programs are also commonly used only for monitoring/tuning.
        // They are tagged only when a visible benchmark/stress-test window is present.
        new(new[] { "aida64" }, BenchmarkWorkloadKind.Cpu | BenchmarkWorkloadKind.Gpu | BenchmarkWorkloadKind.Memory),
        new(new[] { "cpuz" }, BenchmarkWorkloadKind.Cpu),
        new(new[] { "intelxtu", "xtuui", "xtucli" }, BenchmarkWorkloadKind.Cpu),
        new(new[] { "ryzenmaster" }, BenchmarkWorkloadKind.Cpu),
        new(new[] { "gpuz" }, BenchmarkWorkloadKind.Gpu)
    };

    private static readonly string[] BenchmarkWindowKeywords =
    {
        "benchmark",
        "stresstest",
        "stabilitytest",
        "systemstabilitytest",
        "burnintest",
        "rendertest",
        "memorytest",
        "cputest",
        "gputest"
    };

    public static BenchmarkWorkloadKind Detect(
        HardwareSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        List<RunningProcessInfo> runningProcesses =
            ReadRunningProcesses();

        if (runningProcesses.Count == 0)
        {
            return BenchmarkWorkloadKind.None;
        }

        BenchmarkWorkloadKind detected =
            BenchmarkWorkloadKind.None;

        foreach (RunningProcessInfo process in runningProcesses)
        {
            foreach (BenchmarkProcessRule rule in DedicatedRules)
            {
                if (MatchesProcessName(
                        process.NormalizedName,
                        rule.Patterns))
                {
                    detected |=
                        rule.Workloads;
                }
            }

            foreach (BenchmarkProcessRule rule in ActiveLoadRules)
            {
                if (!MatchesProcessName(
                        process.NormalizedName,
                        rule.Patterns))
                {
                    continue;
                }

                detected |=
                    GetActiveWorkloads(
                        snapshot,
                        rule.Workloads);
            }

            foreach (AmbiguousBenchmarkRule rule in AmbiguousRules)
            {
                if (!MatchesProcessName(
                        process.NormalizedName,
                        rule.Patterns) ||
                    !LooksLikeBenchmarkWindow(
                        process.NormalizedWindowTitle))
                {
                    continue;
                }

                BenchmarkWorkloadKind active =
                    GetActiveWorkloads(
                        snapshot,
                        rule.Workloads);

                detected |=
                    active == BenchmarkWorkloadKind.None
                        ? rule.Workloads
                        : active;
            }
        }

#if DEBUG
        if (detected != BenchmarkWorkloadKind.None)
        {
            Debug.WriteLine(
                $"[Thermiqra] Test workload detected: {detected}");
        }
#endif

        return detected;
    }

    private static BenchmarkWorkloadKind GetActiveWorkloads(
        HardwareSnapshot snapshot,
        BenchmarkWorkloadKind allowed)
    {
        BenchmarkWorkloadKind result =
            BenchmarkWorkloadKind.None;

        if ((allowed & BenchmarkWorkloadKind.Cpu) != 0 &&
            snapshot.Cpu.Load.HasValue &&
            snapshot.Cpu.Load.Value >= CpuActiveThreshold)
        {
            result |=
                BenchmarkWorkloadKind.Cpu;
        }

        if ((allowed & BenchmarkWorkloadKind.Gpu) != 0)
        {
            bool gpuActive =
                snapshot.Gpus.Any(
                    gpu =>
                        (gpu.Load.HasValue &&
                         gpu.Load.Value >= GpuActiveThreshold) ||
                        (gpu.MemoryLoad.HasValue &&
                         gpu.MemoryLoad.Value >= GpuMemoryActiveThreshold));

            if (gpuActive)
            {
                result |=
                    BenchmarkWorkloadKind.Gpu;
            }
        }

        if ((allowed & BenchmarkWorkloadKind.Memory) != 0 &&
            snapshot.Memory.Load.HasValue &&
            snapshot.Memory.Load.Value >= MemoryActiveThreshold)
        {
            result |=
                BenchmarkWorkloadKind.Memory;
        }

        if ((allowed & BenchmarkWorkloadKind.Storage) != 0)
        {
            result |=
                BenchmarkWorkloadKind.Storage;
        }

        return result;
    }

    private static bool MatchesProcessName(
        string normalizedProcessName,
        IReadOnlyList<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(
                normalizedProcessName))
        {
            return false;
        }

        foreach (string pattern in patterns)
        {
            string normalizedPattern =
                Normalize(pattern);

            if (normalizedProcessName.Equals(
                    normalizedPattern,
                    StringComparison.Ordinal) ||
                normalizedProcessName.StartsWith(
                    normalizedPattern,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeBenchmarkWindow(
        string normalizedWindowTitle)
    {
        if (string.IsNullOrWhiteSpace(
                normalizedWindowTitle))
        {
            return false;
        }

        return BenchmarkWindowKeywords.Any(
            keyword =>
                normalizedWindowTitle.Contains(
                    keyword,
                    StringComparison.Ordinal));
    }

    private static List<RunningProcessInfo> ReadRunningProcesses()
    {
        Dictionary<int, string> windowTitles =
            ReadTopLevelWindowTitles();

        List<RunningProcessInfo> result =
            new();

        Process[] processes;

        try
        {
            processes =
                Process.GetProcesses();
        }
        catch
        {
            return result;
        }

        foreach (Process process in processes)
        {
            try
            {
                string processName =
                    Normalize(
                        process.ProcessName);

                if (string.IsNullOrWhiteSpace(
                        processName))
                {
                    continue;
                }

                string windowTitle =
                    windowTitles.TryGetValue(
                        process.Id,
                        out string? collectedTitle)
                        ? collectedTitle
                        : Normalize(
                            process.MainWindowTitle);

                result.Add(
                    new RunningProcessInfo(
                        processName,
                        windowTitle));
            }
            catch
            {
                // Some protected/system processes cannot be inspected.
                // Ignoring them is safer than breaking monitoring.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    private static Dictionary<int, string> ReadTopLevelWindowTitles()
    {
        Dictionary<int, string> result =
            new();

        try
        {
            EnumWindows(
                (windowHandle, unusedParameter) =>
                {
                    if (!IsWindowVisible(
                            windowHandle))
                    {
                        return true;
                    }

                    int length =
                        GetWindowTextLength(
                            windowHandle);

                    if (length <= 0)
                    {
                        return true;
                    }

                    StringBuilder builder =
                        new(length + 1);

                    if (GetWindowText(
                            windowHandle,
                            builder,
                            builder.Capacity) <= 0)
                    {
                        return true;
                    }

                    GetWindowThreadProcessId(
                        windowHandle,
                        out uint processId);

                    if (processId == 0)
                    {
                        return true;
                    }

                    string normalizedTitle =
                        Normalize(
                            builder.ToString());

                    if (string.IsNullOrWhiteSpace(
                            normalizedTitle))
                    {
                        return true;
                    }

                    int id =
                        unchecked((int)processId);

                    if (result.TryGetValue(
                            id,
                            out string? existing))
                    {
                        result[id] =
                            existing + " " + normalizedTitle;
                    }
                    else
                    {
                        result[id] =
                            normalizedTitle;
                    }

                    return true;
                },
                IntPtr.Zero);
        }
        catch
        {
            // Window-title inspection is only an extra safeguard for ambiguous
            // utilities. Dedicated benchmark detection still works without it.
        }

        return result;
    }

    private static string Normalize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return "";
        }

        StringBuilder builder =
            new(value.Length);

        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(
                    char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private sealed class BenchmarkProcessRule
    {
        public IReadOnlyList<string> Patterns { get; }

        public BenchmarkWorkloadKind Workloads { get; }

        public BenchmarkProcessRule(
            IReadOnlyList<string> patterns,
            BenchmarkWorkloadKind workloads)
        {
            Patterns = patterns;
            Workloads = workloads;
        }
    }

    private sealed class AmbiguousBenchmarkRule
    {
        public IReadOnlyList<string> Patterns { get; }

        public BenchmarkWorkloadKind Workloads { get; }

        public AmbiguousBenchmarkRule(
            IReadOnlyList<string> patterns,
            BenchmarkWorkloadKind workloads)
        {
            Patterns = patterns;
            Workloads = workloads;
        }
    }

    private sealed class RunningProcessInfo
    {
        public string NormalizedName { get; }

        public string NormalizedWindowTitle { get; }

        public RunningProcessInfo(
            string normalizedName,
            string normalizedWindowTitle)
        {
            NormalizedName = normalizedName;
            NormalizedWindowTitle = normalizedWindowTitle;
        }
    }

    private delegate bool EnumWindowsProc(
        IntPtr hWnd,
        IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(
        EnumWindowsProc lpEnumFunc,
        IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(
        IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr hWnd,
        StringBuilder lpString,
        int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(
        IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint lpdwProcessId);
}
