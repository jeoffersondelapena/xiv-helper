using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace XIVHelper.Demiatma;

/// <summary>Every few seconds in South Horn: counts the demiatma, keeps BOCCHI's skip lists in step and says what changed.
/// Runs on the frame thread, where BOCCHI reads those lists too.</summary>
internal sealed class DemiatmaWatcher
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Action<string>? diag;
    private readonly string statePath;
    private HashSet<uint> mineFates = new();
    private HashSet<uint> mineCriticalEncounters = new();
    private DateTime lastPoll = DateTime.MinValue;
    private readonly HashSet<DemiatmaKind> announced = new();
    private int lastCompleteCount;
    private bool allAnnounced;
    private string? lastError;

    public DemiatmaWatcher(IDalamudPluginInterface pluginInterface, IClientState clientState, IChatGui chat, IPluginLog log, Action<string>? diag)
    {
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.chat = chat;
        this.log = log;
        this.diag = diag;
        // which ids this plugin put on the lists survives a restart, so another character's poll takes exactly those back out
        statePath = Path.Combine(pluginInterface.ConfigDirectory.FullName, "demiatma-skips.json");
        LoadState();
    }

    public void Tick(DateTime now)
    {
        if ((now - lastPoll).TotalSeconds < DemiatmaWatch.PollSeconds) return;
        lastPoll = now;
        if (!clientState.IsLoggedIn || clientState.TerritoryType != DemiatmaWatch.SouthHorn) return;
        try
        {
            Poll();
            lastError = null;
        }
        catch (Exception ex)
        {
            // once per distinct failure: a renamed member in a BOCCHI or Dalamud update shows up here, every poll retries
            if (lastError == ex.Message) return;
            lastError = ex.Message;
            log.Error(ex, "BOCCHI's skip lists are out of reach");
            diag?.Invoke($"demiatma: lists out of reach: {ex.GetType().Name}: {ex.Message}");
            chat.PrintError("XIV Helper: " + DemiatmaWatch.UnreachableLine(ex.Message));
        }
    }

    /// <summary>One line for /helper demiatma: the counts, and which kinds are being skipped.</summary>
    public string Status()
    {
        var counts = Counts();
        if (counts is null) return "the inventory is not readable yet.";
        string bocchi;
        IReadOnlySet<DemiatmaKind> skipping = new HashSet<DemiatmaKind>();
        try
        {
            var loaded = BocchiLists.Instance(pluginInterface) is not null;
            bocchi = loaded ? "loaded" : "not loaded";
            if (loaded) skipping = DemiatmaWatch.Complete(counts);
        }
        catch (Exception ex)
        {
            bocchi = $"out of reach ({ex.Message})";
        }
        var where = clientState.TerritoryType == DemiatmaWatch.SouthHorn ? "in South Horn" : "outside South Horn, lists untouched";
        return $"{DemiatmaWatch.StatusLine(counts, skipping)}; BOCCHI {bocchi}, {where}.";
    }

    private void Poll()
    {
        var counts = Counts();
        if (counts is null) return;
        var complete = DemiatmaWatch.Complete(counts);
        var instance = BocchiLists.Instance(pluginInterface);
        if (instance is null) return;

        var lists = BocchiLists.Open(instance);
        if (lists is null) return;

        var plan = DemiatmaWatch.Plan(complete, lists.Value.Fates, lists.Value.CriticalEncounters, mineFates, mineCriticalEncounters);
        foreach (var id in plan.RemoveFates) lists.Value.Fates.Remove(id);
        foreach (var id in plan.AddFates) lists.Value.Fates.Add(id);
        foreach (var id in plan.RemoveCriticalEncounters) lists.Value.CriticalEncounters.Remove(id);
        foreach (var id in plan.AddCriticalEncounters) lists.Value.CriticalEncounters.Add(id);
        if (!mineFates.SetEquals(plan.MineFates) || !mineCriticalEncounters.SetEquals(plan.MineCriticalEncounters))
        {
            mineFates = plan.MineFates.ToHashSet();
            mineCriticalEncounters = plan.MineCriticalEncounters.ToHashSet();
            SaveState();
        }
        if (plan.Changed)
            diag?.Invoke($"demiatma: {DemiatmaWatch.StatusLine(counts, complete)}; skips +{plan.AddFates.Count + plan.AddCriticalEncounters.Count} -{plan.RemoveFates.Count + plan.RemoveCriticalEncounters.Count}");

        Announce(counts, complete, plan);
    }

    private void Announce(IReadOnlyDictionary<DemiatmaKind, int> counts, IReadOnlySet<DemiatmaKind> complete, SkipPlan plan)
    {
        var handedIn = lastCompleteCount == DemiatmaWatch.Kinds.Count && complete.Count == 0;
        lastCompleteCount = complete.Count;
        foreach (var kind in DemiatmaWatch.Kinds)
        {
            if (complete.Contains(kind))
            {
                if (announced.Add(kind)) Say(DemiatmaWatch.CompleteLine(kind));
            }
            else if (announced.Remove(kind) && !handedIn && Removed(plan, kind))
            {
                Say(DemiatmaWatch.NeededAgainLine(kind, counts.GetValueOrDefault(kind)));
            }
        }
        if (handedIn)
            Say("demiatma handed in; BOCCHI's FATE and critical encounter lists are back to normal.");
        if (complete.Count == DemiatmaWatch.Kinds.Count && !allAnnounced)
            Say(DemiatmaWatch.AllCompleteLine);
        allAnnounced = complete.Count == DemiatmaWatch.Kinds.Count;
    }

    private static bool Removed(SkipPlan plan, DemiatmaKind kind) =>
        plan.RemoveFates.Any(id => DemiatmaWatch.Fates[id] == kind) || plan.RemoveCriticalEncounters.Any(id => DemiatmaWatch.CriticalEncounters[id] == kind);

    private void Say(string line)
    {
        chat.Print("XIV Helper: " + line);
        diag?.Invoke("demiatma: " + line);
    }

    /// <summary>True once every kind is at three in the inventory; false while the inventory is not readable.</summary>
    public static bool AllComplete()
    {
        var counts = Counts();
        return counts is not null && DemiatmaWatch.Complete(counts).Count == DemiatmaWatch.Kinds.Count;
    }

    private static unsafe Dictionary<DemiatmaKind, int>? Counts()
    {
        var inventory = InventoryManager.Instance();
        if (inventory == null) return null;
        var counts = new Dictionary<DemiatmaKind, int>();
        foreach (var kind in DemiatmaWatch.Kinds)
            counts[kind] = inventory->GetInventoryItemCount((uint)kind);
        return counts;
    }

    private sealed class State
    {
        public uint[] Fates { get; set; } = Array.Empty<uint>();
        public uint[] CriticalEncounters { get; set; } = Array.Empty<uint>();
    }

    private void LoadState()
    {
        try
        {
            if (!File.Exists(statePath)) return;
            var state = JsonSerializer.Deserialize<State>(File.ReadAllText(statePath));
            mineFates = state?.Fates.ToHashSet() ?? new HashSet<uint>();
            mineCriticalEncounters = state?.CriticalEncounters.ToHashSet() ?? new HashSet<uint>();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "demiatma skip record unreadable; earlier skips will be treated as the player's own");
        }
    }

    private void SaveState()
    {
        try
        {
            var state = new State { Fates = mineFates.Order().ToArray(), CriticalEncounters = mineCriticalEncounters.Order().ToArray() };
            File.WriteAllText(statePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "demiatma skip record not written; skips made now may not be taken back after a restart");
        }
    }
}
