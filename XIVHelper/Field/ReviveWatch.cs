namespace XIVHelper.Field;

public enum ReviveMove
{
    None,
    AcceptRaise,
    ConfirmReturn,
    AskReturn,
}

/// <summary>What to do with the revive dialogs while the character lies dead in the Occult Crescent. BOCCHI itself only waits
/// to be raised, with no way back to camp on its own.</summary>
public static class ReviveWatch
{
    public static readonly IReadOnlySet<uint> Zones = new HashSet<uint> { 1252, 1346 };
    public const int DefaultReturnAfterSeconds = 120;
    public const double PollSeconds = 0.5;
    public const double AskAgainSeconds = 5;
    public const ushort Transcendent = 418;

    // the auto-translate phrase "Thank you." (Completion sheet, group 2, row 216), so every reader sees it in their own language
    public const uint ThanksGroup = 2;
    public const uint ThanksKey = 216;

    /// <summary>AutoDuty's rule: the only yes/no that shows while the revive agent's own dialog is down is a raise offer, so it is
    /// accepted at once; the agent's return dialog is confirmed only once the wait is over and no raise is on its way.</summary>
    public static ReviveMove Next(bool dead, double deadForSeconds, bool raiseOffered, bool promptOpen, bool agentShown, double returnAfterSeconds)
    {
        if (!dead) return ReviveMove.None;
        if (promptOpen && !agentShown) return ReviveMove.AcceptRaise;
        if (raiseOffered || deadForSeconds < returnAfterSeconds) return ReviveMove.None;
        if (promptOpen) return ReviveMove.ConfirmReturn;
        return agentShown ? ReviveMove.None : ReviveMove.AskReturn;
    }

    /// <summary>The wait before returning, from the first line of the override file; the default when the file is missing or not a number.</summary>
    public static int ReturnAfterSeconds(string? firstLine) =>
        int.TryParse(firstLine?.Trim(), out var seconds) && seconds > 0 ? seconds : DefaultReturnAfterSeconds;

    public static string ReturnLine(int waitedSeconds) =>
        $"dead for {waitedSeconds} s with no raise; returning to camp. BOCCHI carries on from there.";
}
