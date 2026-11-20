using System;
using System.Diagnostics;

namespace PCHardwareMonitor;

internal static partial class ProgramSystemResourceDiscovery
{
    private static void ReadTasks(Action<string, string, string> add)
    {
        object? service = null, root = null;
        var budget = Stopwatch.StartNew(); int count = 0;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service") ?? throw new InvalidOperationException());
            dynamic scheduler = service!; scheduler.Connect(); root = scheduler.GetFolder("\\");
            void Folder(dynamic folder, int depth)
            {
                Limit(budget, ++count, 3000);
                if (depth > 8) throw new System.IO.IOException(SettingsService.L("Превышена допустимая глубина папок планировщика заданий.", "Task folder depth exceeded."));
                object? tasks = null, folders = null;
                try
                {
                    tasks = folder.GetTasks(1);
                    dynamic taskList = tasks;
                    for (int i = 1; i <= (int)taskList.Count; i++)
                    {
                        Limit(budget, ++count, 3000);
                        object? task = null, definition = null, actions = null;
                        try
                        {
                            task = taskList.Item(i); dynamic entry = task;
                            definition = entry.Definition; dynamic model = definition;
                            actions = model.Actions; dynamic actionList = actions;
                            for (int j = 1; j <= (int)actionList.Count; j++)
                            {
                                Limit(budget, ++count, 3000);
                                object? action = null;
                                try
                                {
                                    action = actionList.Item(j); dynamic command = action;
                                    if ((int)command.Type == 0)
                                        add("ScheduledTask", "Task · " + (string)entry.Path, '"' + (string)command.Path + '"');
                                }
                                finally { Release(action); }
                            }
                        }
                        finally { Release(actions); Release(definition); Release(task); }
                    }
                    folders = folder.GetFolders(0); dynamic children = folders;
                    for (int i = 1; i <= (int)children.Count; i++)
                    {
                        object? child = null;
                        try { child = children.Item(i); Folder(child, depth + 1); }
                        finally { Release(child); }
                    }
                }
                finally { Release(tasks); Release(folders); }
            }
            Folder(root, 0);
        }
        finally { Release(root); Release(service); }
    }

    private static void ReadFirewall(Action<string, string, string> add)
    {
        object? policy = null, rules = null;
        var budget = Stopwatch.StartNew(); int count = 0;
        try
        {
            policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2") ?? throw new InvalidOperationException());
            dynamic firewall = policy!; rules = firewall.Rules;
            foreach (object rule in (System.Collections.IEnumerable)rules!)
            {
                try
                {
                    Limit(budget, ++count, 10000);
                    dynamic item = rule;
                    string application = (string?)item.ApplicationName ?? "";
                    if (application.Length > 0) add("FirewallRule", "Firewall · " + (string)item.Name, '"' + application.Trim('"') + '"');
                }
                finally { Release(rule); }
            }
        }
        finally { Release(rules); Release(policy); }
    }
}
