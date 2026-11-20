using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace PCHardwareMonitor;

internal static class ProgramLeftoverDiscoveryService
{
    internal static ProgramRemovalBaseline Capture(InstalledProgramInfo app,
        InstalledProgramUninstallPlan plan, IReadOnlyList<InstalledProgramInfo> inventory)
    {
        List<ProgramLeftoverProbe> probes = new();
        List<ProgramReviewLimitation> limitations = new();
        string root = ProgramLeftoverPathPolicy.Normalize(app.InstallLocation);
        bool broad = root.Length == 0 || InstalledProgramLocationService.IsBroadContainerDirectory(root);
        bool overlap = root.Length > 0 && inventory.Any(other => other.Identity != app.Identity &&
            !string.IsNullOrWhiteSpace(other.InstallLocation) &&
            (ProgramLeftoverPathPolicy.Inside(ProgramLeftoverPathPolicy.Normalize(other.InstallLocation), root) ||
             (!InstalledProgramLocationService.IsBroadContainerDirectory(other.InstallLocation) &&
              ProgramLeftoverPathPolicy.Inside(root, ProgramLeftoverPathPolicy.Normalize(other.InstallLocation)))));
        bool userContent = root.Length > 0 && ProgramLeftoverPathPolicy.UserContent(root);
        if (root.Length > 0)
            probes.Add(new("Directory", root,
                broad || overlap || userContent || app.Kind == "MSIX" ? ProgramLeftoverGroup.Protected : ProgramLeftoverGroup.Related,
                userContent ? "Пользовательские файлы: исключены из очистки." : broad || overlap
                    ? "Общий каталог или пересечение с другой установкой: исключён из очистки."
                    : app.Kind == "MSIX" ? "Каталог пакета управляется Windows: ручная очистка исключена."
                    : "Папка установки из регистрации Windows. Содержимое и возможность очистки проверяются после удаления программы.",
                userContent ? "User files: excluded from cleanup." : broad || overlap
                    ? "Shared directory or overlap with another installation: excluded from cleanup."
                    : app.Kind == "MSIX" ? "Windows-managed package directory: manual cleanup excluded."
                    : "Installation folder from Windows registration. Contents and cleanup eligibility are checked after uninstall."));
        HashSet<string> aliases = new(StringComparer.OrdinalIgnoreCase);
        if (!broad && !overlap && !userContent) aliases.Add(Key(Path.GetFileName(root)));
        aliases.Add(Key(app.Name));
        // Installer titles often append a version and architecture to the
        // product name. These aliases are used for review, never ownership.
        string productName = ProgramUserDataLeftoverService.ProductName(app.Name);
        aliases.Add(Key(productName));
        aliases.Add(Key(ProgramProductIdentity.RegistrationName(app.Identity)));
        if (ProgramProductDataLayout.Derive(app.Name, root) is { } layout) aliases.Add(Key(layout.Product));
        string[] genericNames = { "microsoft", "windows", "software", "classes", "policies", "clients",
            "programs", "games", "common", "commonfiles", "packages", "appdata", "programdata",
            "application", "applications", "data", "documents" };
        aliases.RemoveWhere(value => value.Length < 3 || genericNames.Contains(value, StringComparer.OrdinalIgnoreCase));
        if (app.Kind == "MSIX")
        {
            string family = app.Identity[5..];
            probes.Add(new("Directory", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", family),
                ProgramLeftoverGroup.Protected, "Данные пакета могут содержать настройки и пользовательские файлы: исключены из очистки.",
                "Package data may contain settings and user files: excluded from cleanup."));
        }
        else
        {
            string[] parts = app.Identity.Split(':', 4);
            if (parts.Length == 4)
                probes.Add(new("Registry", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + parts[3],
                    ProgramLeftoverGroup.NeedsReview, "Исходная запись удаления всё ещё существует: требуется проверка, а не автоматическое удаление.",
                    "Original uninstall registration still exists: review is required, not automatic deletion.", parts[1], parts[2]));
        }
        Stopwatch budget = Stopwatch.StartNew();
        foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.CommonApplicationData })
        {
            string parent = Environment.GetFolderPath(folder);
            if (InstalledProgramLocationService.HasReparseAncestor(parent))
            {
                limitations.Add(new("Переход по ссылке в каталоге данных исключён", "Data-directory link traversal excluded", parent));
                continue;
            }
            try
            {
                int visited = 0;
                foreach (string directory in Directory.EnumerateDirectories(parent))
                {
                    if (++visited > 1000 || budget.ElapsedMilliseconds > 3000)
                    { limitations.Add(new("Поиск каталогов данных ограничен", "Data-directory search was bounded")); break; }
                    if (!aliases.Contains(Key(Path.GetFileName(directory)))) continue;
                    probes.Add(new("Directory", directory, ProgramLeftoverGroup.Protected,
                        "Совпадает имя каталога данных. Здесь могут быть настройки, сохранения и документы: исключён из очистки.",
                        "Data-directory name matches. It may contain settings, saves or documents: excluded from cleanup."));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { limitations.Add(new("Каталог данных недоступен", "Data directory inaccessible", parent + ": " + ex.Message)); }
        }
        List<string> projects = new();
        if (!broad && !overlap && !userContent && app.Kind != "MSIX")
            try { projects = ProgramGameDataLocations.LiveAliases(root); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            { limitations.Add(new("Не удалось проверить структуру игрового проекта", "Game project layout could not be checked", ex.Message)); }
        ProgramUserDataLeftoverService.Capture(app, inventory, probes, limitations, projects);
        CaptureRegistry(probes, limitations, aliases, Key(app.Publisher), root, broad || overlap || userContent, budget);
        if (!broad && !overlap && !userContent && app.Kind != "MSIX")
            ProgramShortcutLeftoverService.Capture(root, probes, limitations);
        return new(Guid.NewGuid().ToString("N"), DateTime.UtcNow, plan.Identity, plan.Name, plan.Version,
            probes.Select(ProgramRegistryIdentity.Canonical).DistinctBy(ProgramRegistryIdentity.Key, StringComparer.OrdinalIgnoreCase).ToList(), limitations,
            app.Publisher, root, GameProjects: projects);
    }

    private static void CaptureRegistry(List<ProgramLeftoverProbe> probes, List<ProgramReviewLimitation> limitations,
        HashSet<string> aliases, string publisher, string root, bool protectedRoot, Stopwatch budget)
    {
        budget = Stopwatch.StartNew(); // Registry discovery has its own bounded budget.
        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            string scope = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
            string bits = view == RegistryView.Registry64 ? "64" : "32";
            try
            {
                using RegistryKey registry = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? software = registry.OpenSubKey("SOFTWARE", false);
                int organizations = 0;
                if (software != null)
                    foreach (string name in software.GetSubKeyNames())
                    {
                        if (++organizations > 2000 || budget.ElapsedMilliseconds > 4000)
                        { limitations.Add(new("Поиск организаций в реестре ограничен", "Registry organization search was bounded", scope)); break; }
                        if (aliases.Contains(Key(name)))
                            probes.Add(new("Registry", @"SOFTWARE\" + name, ProgramLeftoverGroup.NeedsReview,
                                "Только совпадение имени раздела. Принадлежность одной программе не доказана.",
                                "Registry key name matches only. Ownership by one application is not established.", scope, bits));
                        // A common layout is Software\Publisher\Application.
                        // The settings organization can differ from the
                        // installer's Publisher. Match an exact application
                        // child, never assume the whole organization is owned.
                        if ((publisher.Length < 3 || Key(name) != publisher) &&
                            new[] { "microsoft", "windows", "classes", "policies", "wow6432node" }.Contains(Key(name))) continue;
                        try
                        {
                            using RegistryKey? vendor = software.OpenSubKey(name, false);
                            if (vendor == null) continue;
                            int children = 0;
                            foreach (string child in vendor.GetSubKeyNames())
                            {
                                if (++children > 200 || budget.ElapsedMilliseconds > 4000)
                                { limitations.Add(new("Поиск в разделе организации ограничен", "Organization-key search was bounded", scope + "\\SOFTWARE\\" + name)); break; }
                                if (aliases.Contains(Key(child)))
                                    probes.Add(new("Registry", @"SOFTWARE\" + name + "\\" + child, ProgramLeftoverGroup.NeedsReview,
                                        "Найден раздел приложения внутри организации. Здесь могут быть настройки и пользовательские данные; принадлежность требует проверки.",
                                        "An application key was found under an organization. Settings and user data may be present; ownership requires review.", scope, bits));
                            }
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                        { limitations.Add(new("Раздел организации недоступен", "Organization key is inaccessible", scope + "\\SOFTWARE\\" + name)); }
                    }
                if (protectedRoot) continue;
                const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
                using RegistryKey? paths = registry.OpenSubKey(appPaths, false);
                if (paths == null) continue;
                int count = 0;
                foreach (string name in paths.GetSubKeyNames())
                {
                    if (++count > 1000 || budget.ElapsedMilliseconds > 4000)
                    { limitations.Add(new("Поиск App Paths ограничен", "App Paths search was bounded", scope)); break; }
                    using RegistryKey? entry = paths.OpenSubKey(name, false);
                    string executable = ProgramLeftoverPathPolicy.Normalize(Convert.ToString(entry?.GetValue(null)) ?? "");
                    if (executable.Length > 0 && ProgramLeftoverPathPolicy.Inside(executable, root))
                        probes.Add(new("Registry", appPaths + "\\" + name, ProgramLeftoverGroup.Related,
                            "App Paths указывал на файл внутри каталога программы до удаления; необходимо проверить других пользователей этой записи.",
                            "App Paths pointed to a file inside the application directory before uninstall; other users of this registration must be reviewed.", scope, bits, root));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { limitations.Add(new("Источник реестра недоступен", "Registry source inaccessible", scope + " (" + bits + "): " + ex.Message)); }
        }
    }

    private static string Key(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    internal static ProgramLeftoverReport Inspect(ProgramRemovalBaseline baseline)
    {
        if (baseline.NativeResult != null)
            baseline = baseline with { NativeResult = ProgramRemovalRebootPolicy.Refresh(
                ProgramPendingProcesses.Refresh(baseline.NativeResult), baseline.CapturedUtc,
                (baseline.NativeResult.State == InstalledProgramRemovalState.RebootRequired || baseline.NativeResult.HelperObservationIncomplete) && baseline.NativeResult.RegistrationAbsent
                    ? ProgramRemovalRebootPolicy.ReadCurrentBootUtc() ?? DateTime.MinValue : DateTime.MinValue) };
        baseline = ProgramUserDataLeftoverService.ExtendPackageData(baseline);
        baseline = ProgramProductDataLayout.Extend(baseline);
        baseline = ProgramDataFileIdentity.Extend(baseline);
        try { baseline = ProgramGameDataLocations.Extend(baseline); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or System.Security.SecurityException)
        { baseline = baseline with { Limitations = baseline.Limitations.Concat(new[] {
            new ProgramReviewLimitation("Связи игровых профилей из снимка не удалось проверить", "Recorded game profile associations could not be checked", ex.Message) }).ToList() }; }
        baseline = baseline with { Probes = baseline.Probes.Select(ProgramRegistryIdentity.Canonical)
            .DistinctBy(ProgramRegistryIdentity.Key, StringComparer.OrdinalIgnoreCase).ToList() };
        List<ProgramLeftoverProbe> found = new();
        List<ProgramReviewLimitation> limitations = new(baseline.Limitations);
        if (baseline.NativeResult?.HelperObservationIncomplete == true)
            limitations.Add(new("Завершение вспомогательных процессов не подтверждено; требуется перезагрузка и новый анализ.",
                "Helper completion is unconfirmed; restart Windows and inspect again."));
        if ((baseline.NativeResult?.PendingHelpers?.Count ?? 0) > 0)
            limitations.Add(new("Вспомогательные процессы удаления ещё выполняются. Повторите поиск после их завершения.",
                "Uninstall helper processes are still running. Search again after they finish."));
        List<ProgramLeftoverProbe> updatedProbes = baseline.Probes.ToList();
        List<ProgramLeftoverCheck> checks = new();
        var dataPaths = baseline.Probes.Where(p => p.Kind == "UserData").Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> recordedFolders = new(StringComparer.OrdinalIgnoreCase);
        int checkedObjects = 0;
        foreach (ProgramLeftoverProbe probe in baseline.Probes.OrderBy(p => p.Kind == "RecordedFile" ? 1 : 0))
        {
            int foundBefore = found.Count;
            string outcome = "Absent", detail = "";
            try
            {
                if (probe.Kind == "Directory" && dataPaths.Contains(probe.Path))
                { outcome = "CoveredByUserData"; continue; }
                checkedObjects++;
                if (probe.Kind == "RecordedFile")
                {
                    if (recordedFolders.Contains(probe.EvidenceRoot)) continue;
                    File.GetAttributes(probe.Path);
                    try { ProgramRecordedInstallCleanup.Validate(probe, baseline); found.Add(probe); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
                    { found.Add(probe with { Group = ProgramLeftoverGroup.NeedsReview, ReasonRu = "Файл изменился или его очистка не подтверждена.", ReasonEn = "The file changed or cleanup is unconfirmed." }); }
                    continue;
                }
                if (probe.Kind == "UserData")
                {
                    // Check the root itself here, separately from reading its
                    // configuration. Missing dependencies/children must never
                    // be mistaken for an absent application-data directory.
                    FileAttributes dataAttributes = File.GetAttributes(probe.Path);
                    detail = dataAttributes.ToString();
                    var data = ProgramUserDataLeftoverService.Inspect(probe, baseline, limitations);
                    found.Add(data);
                    updatedProbes[updatedProbes.IndexOf(probe)] = data;
                    continue;
                }
                if (probe.Kind == "Shortcut")
                {
                    found.Add(ProgramShortcutLeftoverService.Inspect(probe));
                    continue;
                }
                if (probe.Kind == "Registry")
                {
                    using RegistryKey registry = RegistryKey.OpenBaseKey(probe.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                        probe.View == "64" ? RegistryView.Registry64 : RegistryView.Registry32);
                    using RegistryKey? key = registry.OpenSubKey(probe.Path, false);
                    if (key != null)
                    {
                        var classified = ProgramSettingsRegistryCleanup.Classify(probe, baseline);
                        found.Add(classified);
                        updatedProbes[updatedProbes.IndexOf(probe)] = classified;
                    }
                    continue;
                }
                // Never follow a junction higher in the path, including paths
                // whose leaf disappeared during native uninstall.
                DirectoryInfo? ancestor = new(probe.Path);
                bool linked = false;
                while (ancestor != null)
                {
                    try
                    {
                        if ((File.GetAttributes(ancestor.FullName) & FileAttributes.ReparsePoint) != 0)
                        { linked = true; break; }
                    }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                    ancestor = ancestor.Parent;
                }
                if (linked)
                {
                    found.Add(probe with { Group = ProgramLeftoverGroup.Protected,
                        ReasonRu = "Ссылка или точка соединения: переход и очистка исключены; наличие целевых файлов не проверялось.",
                        ReasonEn = "Link or junction: traversal and cleanup excluded; target files were not inspected." });
                    continue;
                }
                // GetAttributes distinguishes absence from denied access.
                FileAttributes attributes = File.GetAttributes(probe.Path);
                if ((attributes & FileAttributes.Directory) != 0 &&
                    (probe.Group == ProgramLeftoverGroup.Related ||
                     probe.Group == ProgramLeftoverGroup.UserData && probe.EvidenceTarget == ProgramRecordedInstallCleanup.DirectoryEvidence))
                {
                    try
                    {
                        if (baseline.ObservationOnly && !ProgramLeftoverCleanupPolicy.IsEmptyDirectoryTree(probe.Path))
                            ProgramLeftoverCleanupPolicy.Deny();
                        if (probe.EvidenceTarget == ProgramRecordedInstallCleanup.DirectoryEvidence)
                        {
                            var current = ProgramRecordedInstallCleanup.ClassifyDirectory(probe, baseline);
                            found.Add(current); updatedProbes[updatedProbes.IndexOf(probe)] = current;
                            recordedFolders.Add(probe.Path); continue;
                        }
                        ProgramLeftoverCleanupPolicy.CheckDirectory(probe.Path);
                        bool empty = !Directory.EnumerateFileSystemEntries(probe.Path).Any();
                        found.Add(probe with
                        {
                            ReasonRu = empty ? "Пустая папка, оставшаяся после удаления программы. Можно отправить в Корзину." :
                                "Папка установки содержит только исполняемые файлы и библиотеки. Перед очисткой содержимое и использование другими программами проверяются повторно.",
                            ReasonEn = empty ? "Empty folder left after uninstall. It can be sent to Recycle Bin." :
                                "The installation folder contains only executable files and libraries. Contents and use by other applications are checked again before cleanup."
                        });
                    }
                    catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        try
                        {
                            var current = ProgramRecordedInstallCleanup.ClassifyDirectory(probe, baseline);
                            found.Add(current); updatedProbes[updatedProbes.IndexOf(probe)] = current;
                            recordedFolders.Add(probe.Path); continue;
                        }
                        catch (Exception blocked) when (blocked is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
                        found.Add(probe with { Group = ProgramLeftoverGroup.NeedsReview,
                            ReasonRu = "Автоматическая очистка этой папки недоступна: здесь могут быть настройки, личные файлы или неподдерживаемые объекты. Откройте её для проверки.",
                            ReasonEn = "Automatic cleanup is unavailable: settings, personal files or unsupported items may be present. Open the folder to review it." });
                        try
                        {
                            foreach (var file in ProgramRecordedInstallCleanup.ClassifyFiles(probe, baseline))
                            {
                                if (updatedProbes.Count >= 5000)
                                { limitations.Add(new("Список отдельных файлов ограничен", "Individual file list was bounded")); break; }
                                if (!updatedProbes.Any(p => p.Kind == file.Kind && p.Path.Equals(file.Path, StringComparison.OrdinalIgnoreCase)))
                                { found.Add(file); updatedProbes.Add(file); }
                            }
                        }
                        catch (Exception blocked) when (blocked is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException) { }
                    }
                    continue;
                }
                found.Add((attributes & FileAttributes.Directory) != 0 ? probe : probe with
                {
                    Group = ProgramLeftoverGroup.NeedsReview,
                    ReasonRu = "На месте исходного каталога находится файл. Это другой объект; принадлежность не установлена.",
                    ReasonEn = "A file now occupies the former directory path. This is a different object; ownership is not established."
                });
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                detail = ex.GetType().Name + " (" + ex.HResult + "): " + ex.Message;
                // FileNotFound can describe a configuration file or a missing
                // assembly as well as the probe. Confirm the probe's absence
                // before omitting it; never turn an analysis failure into zero.
                if (!ProgramLeftoverCleanupPolicy.AlreadyAbsent(probe))
                {
                    outcome = "Failed";
                    limitations.Add(new("Объект существует или его отсутствие не подтверждено; анализ не завершён",
                        "The object exists or its absence is unconfirmed; analysis did not complete", probe.Path + ": " + ex.Message));
                    if (probe.Kind == "UserData") found.Add(probe with
                    {
                        Group = ProgramLeftoverGroup.NeedsReview,
                        ReasonRu = "Папка не исключена из результатов: её отсутствие не подтверждено. Проверка принадлежности не завершена; очистка недоступна.",
                        ReasonEn = "The folder is retained in the results because its absence is unconfirmed. Ownership inspection did not complete; cleanup is unavailable."
                    });
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
            {
                outcome = "Failed"; detail = ex.GetType().Name + " (" + ex.HResult + "): " + ex.Message;
                limitations.Add(new("Объект не удалось проверить", "Object could not be checked", probe.Path + ": " + ex.Message));
            }
            finally
            {
                checks.Add(new(probe.Kind, probe.Path, found.Count > foundBefore ? "Found" : outcome, detail));
            }
        }
        ProgramSystemResourceDiscovery.Inspect(baseline, found, limitations);
        ProgramRecordedObjectsResult? recorded = null;
        try
        {
            recorded = ProgramRecordedObjectsReview.Inspect(baseline);
            if (recorded != null && (recorded.FilesUnchecked > 0 || recorded.RegistryUnchecked > 0))
                limitations.Add(new("Часть объектов из сохранённого снимка не удалось перепроверить",
                    "Some objects from the saved inventory could not be rechecked"));
            if (recorded != null && found.Count == 0 && (recorded.FilesPresent > 0 || recorded.RegistryPresent > 0))
                limitations.Add(new("Сохранённые объекты ещё присутствуют; анализ расположений требует проверки",
                    "Recorded objects are still present; location analysis requires review"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or System.Text.Json.JsonException)
        { limitations.Add(new("Сохранённый снимок объектов недоступен", "Saved object inventory is unavailable", ex.Message)); }
        return new(baseline with { Probes = updatedProbes }, DateTime.UtcNow, found, limitations, checkedObjects,
            checks, typeof(ProgramLeftoverDiscoveryService).Assembly.Location, recorded);
    }
}
