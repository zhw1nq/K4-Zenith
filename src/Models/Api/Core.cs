using System.Reflection;
using System.Text.RegularExpressions;

public static class CallerIdentifier
{
    private static readonly string CurrentPluginName = Assembly.GetExecutingAssembly().GetName().Name!;
    private static readonly string[] BlockAssemblies = ["System.", "K4-ZenithAPI", "KitsuneMenu"];
    public static readonly List<string> ModuleList = [];

    // Debug flag - set to true to enable logging
    public static bool DebugMode = false;

    // Regex to extract module name from patched method names like ".Zenith_TimeStats.Plugin.UpdatePlaytime_Patch1"
    // Only matches actual module names (TimeStats, Ranks, Stats, CustomTags, etc.)
    // Excludes Core classes: Plugin, Models, Core, Api, Config, etc.
    private static readonly Regex PatchedMethodRegex = new(@"Zenith_(\w+)", RegexOptions.Compiled);
    private static readonly string[] CoreClassNames = ["Plugin", "Models", "Core", "Api", "Config", "ConfigManager", "Player", "Database"];

    public static string GetCallingPluginName()
    {
        var stackTrace = new System.Diagnostics.StackTrace(true);

        if (DebugMode)
        {
            Console.WriteLine($"[CallerIdentifier] Stack trace has {stackTrace.FrameCount} frames:");
            for (int j = 0; j < Math.Min(stackTrace.FrameCount, 15); j++)
            {
                var frame = stackTrace.GetFrame(j);
                var method = frame?.GetMethod();
                var asmName = method?.DeclaringType?.Assembly?.GetName().Name;
                Console.WriteLine($"  [{j}] {asmName} :: {method?.DeclaringType?.Name}.{method?.Name}");
            }
        }

        for (int i = 1; i < stackTrace.FrameCount; i++)
        {
            var method = stackTrace.GetFrame(i)?.GetMethod();
            var assembly = method?.DeclaringType?.Assembly;
            var assemblyName = assembly?.GetName().Name;

            if (assemblyName == "CounterStrikeSharp.API")
                break;

            // Normal case: assembly is properly identified
            if (assemblyName != CurrentPluginName && assemblyName != null && !BlockAssemblies.Any(assemblyName.StartsWith))
            {
                if (!ModuleList.Contains(assemblyName))
                    ModuleList.Add(assemblyName);

                if (DebugMode)
                    Console.WriteLine($"[CallerIdentifier] Returning (normal): {assemblyName}");

                return assemblyName;
            }

            // Handle Harmony-patched methods: assembly is null but method name contains module info
            // E.g., ".Zenith_TimeStats.Plugin.UpdatePlaytime_Patch1" -> extract "K4-Zenith-TimeStats"
            if (assemblyName == null && method != null)
            {
                var declaringTypeName = method.DeclaringType?.FullName ?? method.DeclaringType?.Name ?? "";
                var methodName = method.Name ?? "";
                var fullName = $"{declaringTypeName}.{methodName}";

                var match = PatchedMethodRegex.Match(fullName);
                if (match.Success)
                {
                    var moduleSuffix = match.Groups[1].Value;  // e.g., "TimeStats", "Ranks", "CustomTags"

                    // Skip if this is a Core class, not a module
                    if (CoreClassNames.Contains(moduleSuffix))
                        continue;

                    var derivedModuleName = $"K4-Zenith-{moduleSuffix}";

                    if (DebugMode)
                        Console.WriteLine($"[CallerIdentifier] Derived from patched method: {derivedModuleName}");

                    if (!ModuleList.Contains(derivedModuleName))
                        ModuleList.Add(derivedModuleName);

                    return derivedModuleName;
                }
            }
        }

        if (DebugMode)
            Console.WriteLine($"[CallerIdentifier] No module found, returning Core: {CurrentPluginName}");

        return CurrentPluginName;
    }
}
