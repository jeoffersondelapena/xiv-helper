namespace XIVHelper.Demiatma;

/// <summary>The six demiatma of the first Phantom Weapon step, by item id.</summary>
public enum DemiatmaKind : uint
{
    Azurite = 47744,
    Verdigris = 47745,
    Malachite = 47746,
    Realgar = 47747,
    CaputMortuum = 47748,
    Orpiment = 47749,
}

/// <summary>What BOCCHI's skip lists should gain and lose, and which ids this plugin is then responsible for.</summary>
public sealed record SkipPlan(
    IReadOnlySet<uint> AddFates,
    IReadOnlySet<uint> AddCriticalEncounters,
    IReadOnlySet<uint> RemoveFates,
    IReadOnlySet<uint> RemoveCriticalEncounters,
    IReadOnlySet<uint> MineFates,
    IReadOnlySet<uint> MineCriticalEncounters)
{
    public bool Changed => AddFates.Count + AddCriticalEncounters.Count + RemoveFates.Count + RemoveCriticalEncounters.Count > 0;
}

/// <summary>Which South Horn FATEs and critical encounters BOCCHI should skip once their demiatma is complete. BOCCHI itself only
/// shows the drop next to each activity; its autopilot reads nothing but its two disabled-id lists, so this decides what goes in them.</summary>
public static class DemiatmaWatch
{
    public const ushort SouthHorn = 1252;
    public const int Needed = 3;
    public const int PollSeconds = 5;

    // South Horn tables as in BOCCHI 4.2.0.19 (OccultEventDrops.cs); the ids are the game's own FATE and DynamicEvent ids.
    public static readonly IReadOnlyDictionary<uint, DemiatmaKind> Fates = new Dictionary<uint, DemiatmaKind>
    {
        [1962] = DemiatmaKind.Azurite,      // Rough Waters
        [1963] = DemiatmaKind.Azurite,      // The Golden Guardian
        [1970] = DemiatmaKind.Azurite,      // A Prying Eye
        [1968] = DemiatmaKind.Verdigris,    // A Delicate Balance
        [1969] = DemiatmaKind.Verdigris,    // Sworn to Soil
        [1977] = DemiatmaKind.Verdigris,    // Pleading Pots
        [1966] = DemiatmaKind.Malachite,    // An Unending Duty
        [1965] = DemiatmaKind.Realgar,      // The Winged Terror
        [1967] = DemiatmaKind.Realgar,      // Brain Drain
        [1972] = DemiatmaKind.CaputMortuum, // Serving Darkness
        [1964] = DemiatmaKind.Orpiment,     // King of the Crescent
        [1971] = DemiatmaKind.Orpiment,     // Fatal Allure
        [1976] = DemiatmaKind.Orpiment,     // Persistent Pots
    };

    public static readonly IReadOnlyDictionary<uint, DemiatmaKind> CriticalEncounters = new Dictionary<uint, DemiatmaKind>
    {
        [33] = DemiatmaKind.Azurite,
        [35] = DemiatmaKind.Azurite,
        [36] = DemiatmaKind.Azurite,
        [37] = DemiatmaKind.Verdigris,
        [38] = DemiatmaKind.Malachite,
        [39] = DemiatmaKind.Malachite,
        [47] = DemiatmaKind.Malachite,
        [41] = DemiatmaKind.Realgar,
        [43] = DemiatmaKind.Realgar,
        [45] = DemiatmaKind.Realgar,
        [40] = DemiatmaKind.CaputMortuum,
        [42] = DemiatmaKind.CaputMortuum,
        [46] = DemiatmaKind.CaputMortuum,
        [34] = DemiatmaKind.Orpiment,
        [44] = DemiatmaKind.Orpiment,
    };

    // Pot FATEs also feed BOCCHI's pot-chest farming, so they stay under its own pot settings.
    public static readonly IReadOnlySet<uint> PotFates = new HashSet<uint> { 1976, 1977 };

    public static readonly IReadOnlyList<DemiatmaKind> Kinds = Enum.GetValues<DemiatmaKind>();

    public static string Name(DemiatmaKind kind) => kind == DemiatmaKind.CaputMortuum ? "Caput Mortuum" : kind.ToString();

    public static bool HasPotFate(DemiatmaKind kind) => PotFates.Any(id => Fates[id] == kind);

    public static IReadOnlySet<DemiatmaKind> Complete(IReadOnlyDictionary<DemiatmaKind, int> counts) =>
        Kinds.Where(k => counts.TryGetValue(k, out var n) && n >= Needed).ToHashSet();

    /// <summary>What to add and take back; the player's own entries are left alone.</summary>
    public static SkipPlan Plan(IReadOnlySet<DemiatmaKind> complete,
                                IEnumerable<uint> disabledFates, IEnumerable<uint> disabledCriticalEncounters,
                                IEnumerable<uint> mineFates, IEnumerable<uint> mineCriticalEncounters)
    {
        var wantFates = Fates.Where(p => complete.Contains(p.Value) && !PotFates.Contains(p.Key)).Select(p => p.Key).ToHashSet();
        var wantCes = CriticalEncounters.Where(p => complete.Contains(p.Value)).Select(p => p.Key).ToHashSet();
        var (addF, removeF, keepF) = Diff(wantFates, disabledFates, mineFates);
        var (addC, removeC, keepC) = Diff(wantCes, disabledCriticalEncounters, mineCriticalEncounters);
        return new SkipPlan(addF, addC, removeF, removeC, keepF, keepC);
    }

    private static (HashSet<uint> Add, HashSet<uint> Remove, HashSet<uint> Mine) Diff(HashSet<uint> want, IEnumerable<uint> disabledIds, IEnumerable<uint> mineIds)
    {
        var disabled = disabledIds.ToHashSet();
        var mine = mineIds.ToHashSet();
        var add = want.Where(id => !disabled.Contains(id)).ToHashSet();
        var remove = mine.Where(id => !want.Contains(id) && disabled.Contains(id)).ToHashSet();
        var keep = mine.Where(want.Contains).Concat(add).ToHashSet();
        return (add, remove, keep);
    }

    public static string CompleteLine(DemiatmaKind kind)
    {
        var fates = Fates.Count(p => p.Value == kind && !PotFates.Contains(p.Key));
        var ces = CriticalEncounters.Count(p => p.Value == kind);
        var pots = HasPotFate(kind) ? "; its pot FATE stays under BOCCHI's pot settings" : "";
        return $"{Name(kind)} demiatma complete ({Needed} of {Needed}); BOCCHI skips its {fates} FATEs and {ces} critical encounters from now on{pots}.";
    }

    public static string NeededAgainLine(DemiatmaKind kind, int count) =>
        $"{Name(kind)} demiatma needed again ({count} of {Needed}); its FATEs and critical encounters are back on.";

    public const string AllCompleteLine =
        "all six demiatma complete; hand them to Gerolt in Phantom Village. Every demiatma FATE and critical encounter is off, so BOCCHI returns to camp and waits.";

    public static string UnreachableLine(string detail) =>
        $"cannot reach BOCCHI's FATE and critical encounter lists ({detail}); untick finished demiatma by hand.";

    public static string StatusLine(IReadOnlyDictionary<DemiatmaKind, int> counts, IReadOnlySet<DemiatmaKind> skipping) =>
        string.Join(", ", Kinds.Select(k => $"{Name(k)} {Math.Min(counts.GetValueOrDefault(k), Needed)}/{Needed}{(skipping.Contains(k) ? " (skipped)" : "")}"));
}
