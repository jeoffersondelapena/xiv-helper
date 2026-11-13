namespace XIVHelper.Field;

public enum ReviveMove
{
    None,
    AcceptRaise,
    ConfirmReturn,
    AskReturn,
}

public enum PromptKind
{
    None,
    Raise,
    Return,
    Other,
}

/// <summary>RSR's operating modes as its IPC takes them (RotationSolverReborn.ChangeOperatingMode, a byte enum). Henched is the
/// mode BOCCHI drives it in.</summary>
public enum RsrMode : byte
{
    Off,
    Auto,
    TargetOnly,
    Manual,
    AutoDuty,
    Henched,
    PvP,
}

/// <summary>What to do with the revive dialogs while the character lies dead in the Occult Crescent. BOCCHI itself only waits
/// to be raised, with no way back to camp on its own.</summary>
public static class ReviveWatch
{
    public static readonly IReadOnlySet<uint> Zones = new HashSet<uint> { 1252, 1346 };
    public const int DefaultReturnAfterSeconds = 20;
    public const double PollSeconds = 0.5;
    public const double AskAgainSeconds = 5;
    public const ushort Transcendent = 418;

    // every raise a player can cast, the Phantom Chemist's Revive included, and the Phoenix Down item (an 8 s item cast)
    public static readonly IReadOnlySet<uint> RaiseActions = new HashSet<uint> { 125, 173, 3603, 7523, 24287, 18317, 41634 };
    public const uint PhoenixDown = 4570;

    // the game's own dialog texts (Addon sheet rows), so the prompts are told apart in any client language
    public const uint RaisePromptRow = 112;
    public const uint ReturnPromptRow = 197;
    public const uint HomePointPromptRow = 117;
    public const string RaisePrefixFallback = "Accept Raise";
    public const string ReturnPrefixFallback = "Return to";

    // RSR's auto-off-on-death watches for 0 HP; BOCCHI re-arms RSR in the same instant the revive clears, while HP can still read 0,
    // so RSR switches itself straight off again and BOCCHI, believing it on, never re-arms it until the next activity
    public const double RearmAfterSeconds = 4;

    // the auto-translate phrase "Thank you." (Completion sheet, group 2, row 216), so every reader sees it in their own language
    public const uint ThanksGroup = 2;
    public const uint ThanksKey = 216;

    /// <summary>Accept a raise at once; return only after a full wait with no raise coming and no activity running (a raise there is likely and keeps the credit).</summary>
    public static ReviveMove Next(bool dead, double idleForSeconds, bool raiseIncoming, PromptKind prompt, bool inActivity, double returnAfterSeconds)
    {
        if (!dead) return ReviveMove.None;
        if (prompt == PromptKind.Raise) return ReviveMove.AcceptRaise;
        if (raiseIncoming || inActivity || idleForSeconds < returnAfterSeconds) return ReviveMove.None;
        return prompt switch
        {
            PromptKind.Return => ReviveMove.ConfirmReturn,
            PromptKind.None => ReviveMove.AskReturn,
            _ => ReviveMove.None,
        };
    }

    public static PromptKind Classify(string? prompt, string raisePrefix, string returnPrefix)
    {
        var text = (prompt ?? "").Trim();
        if (text.Length == 0) return PromptKind.None;
        if (text.StartsWith(raisePrefix, StringComparison.OrdinalIgnoreCase)) return PromptKind.Raise;
        if (text.StartsWith(returnPrefix, StringComparison.OrdinalIgnoreCase)) return PromptKind.Return;
        return PromptKind.Other;
    }

    /// <summary>The fixed start of a dialog template: everything before its first placeholder, line break or question mark.</summary>
    public static string Prefix(string? template, string fallback)
    {
        var text = (template ?? "").Trim();
        var cut = text.IndexOfAny(new[] { '\u0002', '\n', '\r', '?' });
        if (cut >= 0) text = text.Substring(0, cut);
        text = text.Trim();
        return text.Length >= 4 ? text : fallback;
    }

    /// <summary>The wait before returning, from the first line of the override file; the default when the file is missing or not a number.</summary>
    public static int ReturnAfterSeconds(string? firstLine) =>
        int.TryParse(firstLine?.Trim(), out var seconds) && seconds > 0 ? seconds : DefaultReturnAfterSeconds;

    public static bool NeedsRearm(bool bocchiLoaded, bool inActivity, bool rsrActive) => bocchiLoaded && inActivity && !rsrActive;

    public const string InFate = "InFate";
    public const string InCriticalEncounter = "InCriticalEncounter";
    public const string InCombat = "InCombat";
    public const double DefendAfterSeconds = 2;
    public const string CePreset = "BOCCHI AI CE";
    public const string MobPreset = "BOCCHI AI MOB";

    /// <summary>BOCCHI arms RSR and its BossMod Reborn targeting preset only inside its activities; a stray mob, or a CE it gave up on
    /// while the character already stood inside, leaves the character standing. During a run, after two seconds of its InCombat
    /// state with either half off, both are armed; BOCCHI switches them off again when the state moves on.</summary>
    public static bool NeedsSelfDefence(string? state, double unarmedForSeconds, bool armed, bool onRun) =>
        onRun && state == InCombat && unarmedForSeconds >= DefendAfterSeconds && !armed;

    /// <summary>BOCCHI's CE and mob presets target anything hostile alike; the name only tells BossMod Reborn's window which fight this is.</summary>
    public static string DefencePreset(bool inCriticalEncounter) => inCriticalEncounter ? CePreset : MobPreset;

    public static bool IsBocchiPreset(string? active) =>
        active is not null && (active.StartsWith("BOCCHI AI ", StringComparison.Ordinal) || active.StartsWith("BOCCHI AR ", StringComparison.Ordinal));

    public const string DefendLine = "a stray fight with nothing armed; RSR and BossMod Reborn set the way BOCCHI sets them.";

    public static string DefenceNote(string preset, DateTime at) =>
        $"BossMod Reborn has no preset named \"{preset}\" since {at:HH:mm}; a stray fight gets RSR alone, which only hits a target picked by hand";

    /// <summary>Whether the death is still "inside an activity": only a death BOCCHI took in a FATE or a CE holds the return, and only
    /// while that activity still runs. A death to a stray mob, or with BOCCHI's state unreadable, follows the game's own tests.</summary>
    public static bool HoldFor(string? diedIn, bool fateRunning, bool ceRunning) => diedIn switch
    {
        InFate => fateRunning,
        InCriticalEncounter => ceRunning,
        null => fateRunning || ceRunning,
        _ => false,
    };

    public const string RearmLine = "RSR had switched itself off on the revive; set back to BOCCHI's mode.";

    // attention notes: lowercase problem, time, consequence; XIV Helper repeats them with the paste-it-on instruction until cleared
    public static string BocchiNote(string problem, DateTime at) =>
        $"cannot tell whether BOCCHI is running since {at:HH:mm} ({problem}); a loaded BOCCHI counts as running until this is fixed";

    public static string ReviveNote(string problem, DateTime at) =>
        $"the revive watch stopped working at {at:HH:mm} ({problem}); raises and returns are yours until it heals";

    public static string ReturnLine(int waitedSeconds) =>
        $"dead for {waitedSeconds} s with no raise; returning to camp. BOCCHI carries on from there.";
}
