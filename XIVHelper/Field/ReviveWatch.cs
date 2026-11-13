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

/// <summary>What to do with the revive dialogs while the character lies dead in the Occult Crescent. BOCCHI itself only waits
/// to be raised, with no way back to camp on its own.</summary>
public static class ReviveWatch
{
    public static readonly IReadOnlySet<uint> Zones = new HashSet<uint> { 1252, 1346 };
    public const int DefaultReturnAfterSeconds = 20;
    public const double PollSeconds = 0.5;
    public const double AskAgainSeconds = 5;
    public const ushort Transcendent = 418;

    // every raise a player can cast, the Phantom Chemist's Revive included
    public static readonly IReadOnlySet<uint> RaiseActions = new HashSet<uint> { 125, 173, 3603, 7523, 24287, 18317, 41634 };

    // the game's own dialog texts (Addon sheet rows), so the prompts are told apart in any client language
    public const uint RaisePromptRow = 112;
    public const uint ReturnPromptRow = 197;
    public const uint HomePointPromptRow = 117;
    public const string RaisePrefixFallback = "Accept Raise";
    public const string ReturnPrefixFallback = "Return to";

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

    public static string ReturnLine(int waitedSeconds) =>
        $"dead for {waitedSeconds} s with no raise; returning to camp. BOCCHI carries on from there.";
}
