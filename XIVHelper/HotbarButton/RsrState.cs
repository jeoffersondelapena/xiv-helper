using System.Reflection;

namespace XIVHelper.HotbarButton;

/// <summary>Rotation Solver Reborn's state: its AutorotationActive IPC when it answers, else its DataCenter statics (names as of RSR 7.5.7.14).</summary>
internal static class RsrState
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static PropertyInfo? state;
    private static PropertyInfo? manual;
    private static DateTime lastLookup = DateTime.MinValue;
    private static DateTime lastAssemblyCheck = DateTime.MinValue;
    private static Assembly? bound;

    /// <summary>Asks RSR over IPC; null when nothing answers. Set by the button, kept out of here so this file stays testable.</summary>
    public static Func<bool?>? Probe { get; set; }

    /// <summary>True while RSR runs, false while it is off, null when RSR is not loaded. The IPC answer comes from the live
    /// instance, so it wins when there is one; the static is the fallback, re-bound when RSR has been reloaded (2026-11-04:
    /// an update mid-session left the old assembly's static frozen at false).</summary>
    public static bool? Running => Probe?.Invoke() ?? Read(ref state, "State");

    public static bool? Manual => Read(ref manual, "IsManual");

    public static string CommandFor(bool running) => running ? "/rotation off" : "/rotation manual";

    private static bool? Read(ref PropertyInfo? property, string name)
    {
        try
        {
            if (property != null && DateTime.UtcNow - lastAssemblyCheck > TimeSpan.FromSeconds(2))
            {
                lastAssemblyCheck = DateTime.UtcNow;
                if (!ReferenceEquals(Current(), bound))
                    state = manual = property = null;   // RSR was reloaded: the old assembly's statics no longer move
            }
            property ??= Bind(name);
            return property?.GetValue(null) as bool?;
        }
        catch (Exception)
        {
            property = null;   // a reloaded RSR brings a new assembly; the next read binds again
            return null;
        }
    }

    private static Assembly? Current()
        => AppDomain.CurrentDomain.GetAssemblies().LastOrDefault(a => a.GetName().Name == "RotationSolver");

    private static PropertyInfo? Bind(string name)
    {
        if (DateTime.UtcNow - lastLookup < TimeSpan.FromSeconds(5))
            return null;
        lastLookup = DateTime.UtcNow;
        bound = Current();
        return bound?.GetType("RotationSolver.Basic.DataCenter")?.GetProperty(name, Static);
    }
}
