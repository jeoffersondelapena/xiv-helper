using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin;

namespace XIVHelper.Field;

// BOCCHI has no IPC, so whether one of its runs is going on is read from its own automator through Dalamud's plugin list and
// BOCCHI's service container (names as of BOCCHI 4.2.0.19 / Ocelot); a renamed member falls back to "loaded" and says so once.
internal static class Bocchi
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static string? reported;

    /// <summary>BOCCHI's plugin object, or null while it is not loaded.</summary>
    public static object? Instance(IDalamudPluginInterface pluginInterface)
    {
        if (PluginControl.Find(pluginInterface, "BOCCHI") is not var (plugin, type)) return null;
        var loaded = type.GetProperty("IsLoaded", Any)?.GetValue(plugin) as bool?
                     ?? throw new InvalidOperationException("Dalamud's plugin record has no IsLoaded");
        if (!loaded) return null;
        var instance = type.GetField("instance", Any)?.GetValue(plugin);
        // Dalamud's field name is its own business: the one object in there that comes from BOCCHI.dll is the plugin.
        instance ??= type.GetFields(Any).Select(f => f.GetValue(plugin)).FirstOrDefault(v => v?.GetType().Assembly.GetName().Name == "BOCCHI");
        return instance;
    }

    /// <summary>True while one of BOCCHI's runs is on (Illegal Mode, Pots &amp; Treasure or Completionist). Loaded but idle is false.
    /// When the automator cannot be read, "loaded" stands in and <paramref name="problem"/> names the missing member, once.</summary>
    public static bool OnRun(IDalamudPluginInterface pluginInterface, out string? problem)
    {
        problem = null;
        var instance = Instance(pluginInterface);
        if (instance is null) return false;
        try
        {
            var running = Running(instance);
            if (running is { } known) return known;
            return false;   // container not up yet: BOCCHI is still loading or unloading
        }
        catch (Exception ex)
        {
            if (reported != ex.Message) problem = reported = ex.Message;
            return true;
        }
    }

    private static bool? Running(object instance)
    {
        var services = Field(instance, "services") as IServiceProvider;
        if (services is null) return null;
        var assembly = AssemblyLoadContext.GetLoadContext(instance.GetType().Assembly)?.Assemblies
                           .FirstOrDefault(a => a.GetName().Name == "BOCCHI.Automator")
                       ?? throw new InvalidOperationException("BOCCHI.Automator is not loaded beside BOCCHI");
        var type = assembly.GetType("BOCCHI.Automator.Services.IAutomator")
                   ?? throw new InvalidOperationException("type BOCCHI.Automator.Services.IAutomator is missing");
        var automator = services.GetService(type) ?? throw new InvalidOperationException("IAutomator is not in BOCCHI's container");
        return type.GetProperty("Enabled")?.GetValue(automator) as bool?
               ?? throw new InvalidOperationException("IAutomator.Enabled is missing");
    }

    private static object? Field(object owner, string name)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Any | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(owner);
        }
        throw new InvalidOperationException($"field {name} is missing on {owner.GetType().Name}");
    }
}
