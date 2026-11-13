namespace XIVHelper.Field;

public enum HornStep
{
    Idle,
    Armed,
    Approach,
    Interact,
    Menu,
    Confirm,
    Commence,
    Entering,
    StartBocchi,
}

/// <summary>Getting back into South Horn after its timer throws the character out, and only then.</summary>
public static class HornWatch
{
    public const uint SouthHorn = 1252;
    public static readonly IReadOnlySet<uint> PhantomVillage = new HashSet<uint> { 1269, 1278 };
    public const uint Jeffroy = 1053611;   // Expedition Coordinator, the NPC that admits players
    public const string MenuText = "South Horn";
    public const string BocchiStart = "/bocchi illegal on";

    public const double PollSeconds = 0.5;
    public const double KickSeconds = 20;          // time left on the instance timer at the moment of leaving that still counts as the timer's doing
    public const double InteractRange = 6;
    public const double StepTimeoutSeconds = 90;
    public const double QueueTimeoutSeconds = 600; // the duty window waits on a free instance
    public const double BocchiDelaySeconds = 5;

    public static bool WasKicked(double? timeLeftAtExit) => timeLeftAtExit is { } t && t <= KickSeconds;

    public static int? MenuIndex(IReadOnlyList<string> entries)
    {
        for (var i = 0; i < entries.Count; i++)
            if (entries[i].Contains(MenuText, StringComparison.OrdinalIgnoreCase)) return i;
        return null;
    }

    public static bool IsConfirmPrompt(string? prompt) => (prompt ?? "").Contains(MenuText, StringComparison.OrdinalIgnoreCase);

    public static double TimeoutFor(HornStep step) => step == HornStep.Commence ? QueueTimeoutSeconds : StepTimeoutSeconds;

    public static bool ParseToggle(string? text) => string.Equals(text?.Trim(), "on", StringComparison.OrdinalIgnoreCase);

    public static string ToggleLine(bool on) => on
        ? "re-entry on: when South Horn's timer throws you out, XIV Helper takes you back in and starts BOCCHI."
        : "re-entry off.";

    public static string KickedLine(double timeLeft) => $"South Horn's timer ran out ({timeLeft:0} s left at exit); going back in.";

    public static string LeftLine(double? timeLeft) => timeLeft is { } t
        ? $"left South Horn with {t:0} s on the timer; not going back in."
        : "left South Horn without a timer reading; not going back in.";

    public static string GaveUpLine(HornStep step) => $"re-entry stopped at {step}; take it from there by hand.";

    public const string EnteredLine = "back in South Horn; BOCCHI started.";
    public const string DoneLine = "all six demiatma complete; not going back in.";
}
