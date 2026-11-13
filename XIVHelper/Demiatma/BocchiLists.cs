using System.Reflection;
using Dalamud.Plugin;

namespace XIVHelper.Demiatma;

// BOCCHI offers no IPC or command for its skip lists and only reads them from disk when it loads, so the live sets are
// reached through its service container (names as of BOCCHI 4.2.0.19 / Ocelot). The sets are CoreLib HashSets, which
// cross the plugin load-context boundary as ISet; a renamed member fails with its name in the message.
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

    /// <summary>The live disabled-id sets, or null while BOCCHI's container is not up (loading or unloading).</summary>
    public static (ISet<uint> Fates, ISet<uint> CriticalEncounters)? Open(object instance)
    {
        var services = Field(instance, "services");
        if (services is null) return null;
        var provider = services as IServiceProvider ?? throw new InvalidOperationException("OcelotPlugin.services is not a service provider");
        var configType = instance.GetType().Assembly.GetType("BOCCHI.Config.Configuration")
                         ?? throw new InvalidOperationException("type BOCCHI.Config.Configuration is missing");
        var config = provider.GetService(configType) ?? throw new InvalidOperationException("Configuration is not in BOCCHI's container");
        var fates = Set(Property(Property(config, "FatesConfig"), "DisabledFateIds"), "FatesConfig.DisabledFateIds");
        var ces = Set(Property(Property(config, "CriticalEncountersConfig"), "DisabledCriticalEncounterIds"), "CriticalEncountersConfig.DisabledCriticalEncounterIds");
        return (fates, ces);
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

    private static object Property(object owner, string name) =>
        owner.GetType().GetProperty(name, Any)?.GetValue(owner)
        ?? throw new InvalidOperationException($"{owner.GetType().Name}.{name} is missing or null");

    private static ISet<uint> Set(object value, string what) =>
        value as ISet<uint> ?? throw new InvalidOperationException($"{what} is a {value.GetType().Name}, not a set of ids");
}
