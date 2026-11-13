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

/// <summary>Counts the demiatma and says when a kind is done; BOCCHI's lists stay untouched, since an idle run gets kicked and the activities pay currency anyway.</summary>
public static class DemiatmaWatch
{
    public const ushort SouthHorn = 1252;
    public const int Needed = 3;
    public const int PollSeconds = 5;

    public static readonly IReadOnlyList<DemiatmaKind> Kinds = Enum.GetValues<DemiatmaKind>();

    public static string Name(DemiatmaKind kind) => kind == DemiatmaKind.CaputMortuum ? "Caput Mortuum" : kind.ToString();

    public static IReadOnlySet<DemiatmaKind> Complete(IReadOnlyDictionary<DemiatmaKind, int> counts) =>
        Kinds.Where(k => counts.TryGetValue(k, out var n) && n >= Needed).ToHashSet();

    public static string CompleteLine(DemiatmaKind kind) => $"{Name(kind)} demiatma complete ({Needed} of {Needed}).";

    public const string AllCompleteLine = "all six demiatma complete; hand them to Gerolt in Phantom Village.";

    public static string StatusLine(IReadOnlyDictionary<DemiatmaKind, int> counts) =>
        string.Join(", ", Kinds.Select(k => $"{Name(k)} {Math.Min(counts.GetValueOrDefault(k), Needed)}/{Needed}"));
}
