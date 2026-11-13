using System.Reflection;
using Dalamud.Plugin;

namespace XIVHelper.Demiatma;

// BOCCHI has no IPC, so "is a BOCCHI run going on" is read from Dalamud's plugin list: loaded or not.
internal static class BocchiLists
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

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
}
