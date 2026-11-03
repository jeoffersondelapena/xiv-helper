using System.Reflection;

namespace XIVHelper.HotbarButton;

/// <summary>Rotation Solver Reborn's state: its AutorotationActive IPC when it answers, else its DataCenter statics (names as of RSR 7.5.7.14).</summary>
internal static class RsrState
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static PropertyInfo? state;
    private static PropertyInfo? manual;
    private static DateTime lastLookup = DateTime.MinValue;

    /// <summary>Asks RSR over IPC; null when nothing answers. Set by the button, kept out of here so this file stays testable.</summary>
    public static Func<bool?>? Probe { get; set; }

    /// <summary>True while RSR runs, false while it is off, null when RSR is not loaded. The static is read first: it is a
    /// plain property, while an IPC call runs RSR's own code on the game thread (a boot froze after that, 2026-11-03).</summary>
    public static bool? Running => Read(ref state, "State") ?? Probe?.Invoke();

    public static bool? Manual => Read(ref manual, "IsManual");

    public static string CommandFor(bool running) => running ? "/rotation off" : "/rotation manual";

    private static bool? Read(ref PropertyInfo? property, string name)
    {
        try
        {
            property ??= Bind(name);
            return property?.GetValue(null) as bool?;
        }
        catch (Exception)
        {
            property = null;   // a reloaded RSR brings a new assembly; the next read binds again
            return null;
        }
    }

    private static PropertyInfo? Bind(string name)
    {
        if (DateTime.UtcNow - lastLookup < TimeSpan.FromSeconds(5))
            return null;
        lastLookup = DateTime.UtcNow;
        var assembly = AppDomain.CurrentDomain.GetAssemblies().LastOrDefault(a => a.GetName().Name == "RotationSolver");
        return assembly?.GetType("RotationSolver.Basic.DataCenter")?.GetProperty(name, Static);
    }
}
