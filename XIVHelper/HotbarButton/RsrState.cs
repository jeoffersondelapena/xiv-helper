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

    /// <summary>True during a BOCCHI run, when switching RSR on means its Henched mode (BOCCHI picks the targets, no auto-off after
    /// combat), not Manual; and the call that sets it. Both set by the plugin, null in tests.</summary>
    public static Func<bool>? BocchiRun { get; set; }
    public static Action? ArmHenched { get; set; }

    public static bool WantsHenched(bool turningOn) => turningOn && BocchiRun?.Invoke() == true && ArmHenched != null;

    /// <summary>True while RSR runs, false while it is off, null when RSR is not loaded. The IPC answer comes from the live
    /// instance, so it wins when there is one; the static is the fallback, re-bound when RSR has been reloaded (2026-11-04:
    /// an update mid-session left the old assembly's static frozen at false).</summary>
    public static bool? Running => Probe?.Invoke() ?? Read(ref state, "State");

    public static bool? Manual => Read(ref manual, "IsManual");

    /// <summary>The game's TerritoryIntendedUse row shared by every Frontline map and by no other zone (game tables, 2026-11-27).</summary>
    public const uint FrontlineUse = 18;

    /// <summary>The intended use of the zone the player stands in; set by the plugin, null in tests.</summary>
    public static Func<uint>? ZoneUse { get; set; }

    /// <summary>Read at every press, so the button follows the zone.</summary>
    public static bool InFrontline
    {
        get
        {
            try { return ZoneUse?.Invoke() == FrontlineUse; }
            catch (Exception) { return false; }   // read inside the hooked UseAction: a failed table lookup must not escape into the game
        }
    }

    /// <summary>Low HP is named in the command; RSR would otherwise use the type its info-bar click last set.</summary>
    public static string StartCommand(bool frontline) => frontline ? "/rotation auto LowHP" : "/rotation manual";

    public static string CommandFor(bool running, bool frontline = false) => running ? "/rotation off" : StartCommand(frontline);

    /// <summary>The command a press sends; null means arm Henched, which a BOCCHI run takes before the zone rule.</summary>
    public static string? PressCommand(bool running, bool frontline)
        => WantsHenched(turningOn: !running) ? null : CommandFor(running, frontline);

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
