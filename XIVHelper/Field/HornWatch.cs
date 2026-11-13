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

/// <summary>Getting back into South or North Horn after its timer throws the character out, and only then.</summary>
public static class HornWatch
{
    public const uint SouthHorn = 1252;
    public const uint NorthHorn = 1346;
    // the coordinator's menu names each horn the way the territory does
    public static readonly IReadOnlyDictionary<uint, string> Horns = new Dictionary<uint, string> { [SouthHorn] = "South Horn", [NorthHorn] = "North Horn" };
    public static readonly IReadOnlySet<uint> PhantomVillage = new HashSet<uint> { 1269, 1278 };
    public const uint Jeffroy = 1053611;   // Expedition Coordinator, the NPC that admits players
    public const string BocchiStart = "/bocchi illegal on";
    public const string LifestreamHome = "auto";   // Lifestream's own idea of home: the house, apartment or inn it is set to

    public const double PollSeconds = 0.5;
    public const double KickSeconds = 20;          // time left on the instance timer at the moment of leaving that still counts as the timer's doing
    public const double InteractRange = 6;
    public const double StepTimeoutSeconds = 90;
    public const double QueueTimeoutSeconds = 600; // the duty window waits on a free instance
    public const double BocchiDelaySeconds = 5;
    public const double HomeDelaySeconds = 2;       // Lifestream cannot teleport while the dialog it was asked after is still closing
    // the yes/no is recognised by its words alone; a party invite is a yes/no too, so a time window would not do
    public static readonly string[] ConfirmMarkers = { "Occult Crescent", "knowledge level" };
    public const double RunMemorySeconds = 60;    // BOCCHI stops its run on the kick's loading screen, before the zone id changes

    public static bool RunWasOn(DateTime? lastRunAt, DateTime now) => lastRunAt is { } t && (now - t).TotalSeconds <= RunMemorySeconds;

    public static bool WasKicked(double? timeLeftAtExit) => timeLeftAtExit is { } t && t <= KickSeconds;

    public static int? MenuIndex(IReadOnlyList<string> entries, string horn)
    {
        for (var i = 0; i < entries.Count; i++)
            if (entries[i].Contains(horn, StringComparison.OrdinalIgnoreCase)) return i;
        return null;
    }

    // the confirmation is a second talk menu whose entries read "Yes." and "No."
    public static int? YesIndex(IReadOnlyList<string> entries) => AnswerIndex(entries, "Yes");

    public static int? NoIndex(IReadOnlyList<string> entries) => AnswerIndex(entries, "No");

    private static int? AnswerIndex(IReadOnlyList<string> entries, string answer)
    {
        for (var i = 0; i < entries.Count; i++)
            if (entries[i].Trim().TrimEnd('.', '。').Equals(answer, StringComparison.OrdinalIgnoreCase)) return i;
        return null;
    }

    public static bool IsConfirmPrompt(string? prompt, string horn)
    {
        var text = prompt ?? "";
        return text.Contains(horn, StringComparison.OrdinalIgnoreCase)
               || ConfirmMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    public static string OneLine(string? text) => string.IsNullOrWhiteSpace(text) ? "(nothing)" : text.Replace("\r", "").Replace('\n', ' ').Trim();

    public static double TimeoutFor(HornStep step) => step == HornStep.Commence ? QueueTimeoutSeconds : StepTimeoutSeconds;

    // on unless the file says off: the re-entry only ever acts on the timer's kick during a BOCCHI run
    public static bool ParseToggle(string? text) => !string.Equals(text?.Trim(), "off", StringComparison.OrdinalIgnoreCase);

    /// <summary>No file means on.</summary>
    public static bool ReadSwitch(bool fileExists, string? firstLine) => !fileExists || ParseToggle(firstLine);

    public static string ToggleLine(bool on) => on
        ? "re-entry on: when a horn's timer throws you out during a BOCCHI run, XIV Helper takes you back in and starts BOCCHI."
        : "re-entry off.";

    public static string KickedLine(string horn, double timeLeft) => $"{horn}'s timer ran out ({timeLeft:0} s left at exit); going back in.";

    public static string LeftLine(string horn, double? timeLeft) => timeLeft is { } t
        ? $"left {horn} with {t:0} s on the timer; not going back in."
        : $"left {horn} without a timer reading; not going back in.";

    public static string GaveUpLine(HornStep step, string detail = "") => $"re-entry stopped at {step}{detail}; take it from there by hand.";

    public static string Describe(IReadOnlyList<string> entries) => entries.Count == 0 ? "(empty)" : string.Join(" | ", entries);

    public static string MenuDetail(IReadOnlyList<string> entries) => $" (the menu offered: {Describe(entries)})";

    public static string GaveUpNote(string reason, DateTime at, bool sentHome) =>
        $"{reason} at {at:HH:mm}; {(sentHome ? "Lifestream was asked to take the character home" : "the character is standing outside the horn, Lifestream could not be asked to take it home")}";

    public static string HomeLine(bool sent) => sent
        ? "asked Lifestream to take the character home."
        : "could not ask Lifestream to take the character home (missing or busy); it is standing outside the horn.";

    public static string NowLine(string horn) => $"going into {horn} now.";
    public const string NotInVillageLine = "re-entry starts from Phantom Village; go there first.";
    public const string OffLine = "re-entry is off; /helper horn on first.";

    public static string BrokenNote(string problem, DateTime at) => $"the re-entry stopped working at {at:HH:mm} ({problem}); go back in by hand";

    public static string EnteredLine(string horn) => $"back in {horn}; BOCCHI started.";
}
