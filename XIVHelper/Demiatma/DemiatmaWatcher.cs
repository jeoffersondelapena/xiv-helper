using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace XIVHelper.Demiatma;

/// <summary>Every few seconds in South Horn: counts the demiatma and says when a kind, or all six, is done.</summary>
internal sealed class DemiatmaWatcher
{
    private readonly IClientState clientState;
    private readonly IChatGui chat;
    private readonly Action<string>? diag;
    private DateTime lastPoll = DateTime.MinValue;
    private readonly HashSet<DemiatmaKind> announced = new();
    private bool allAnnounced;

    public DemiatmaWatcher(IClientState clientState, IChatGui chat, Action<string>? diag)
    {
        this.clientState = clientState;
        this.chat = chat;
        this.diag = diag;
    }

    public void Tick(DateTime now)
    {
        if ((now - lastPoll).TotalSeconds < DemiatmaWatch.PollSeconds) return;
        lastPoll = now;
        if (!clientState.IsLoggedIn || clientState.TerritoryType != DemiatmaWatch.SouthHorn) return;
        var counts = Counts();
        if (counts is null) return;
        var complete = DemiatmaWatch.Complete(counts);
        foreach (var kind in DemiatmaWatch.Kinds)
        {
            if (complete.Contains(kind)) { if (announced.Add(kind)) Say(DemiatmaWatch.CompleteLine(kind)); }
            else announced.Remove(kind);
        }
        if (complete.Count == DemiatmaWatch.Kinds.Count && !allAnnounced) Say(DemiatmaWatch.AllCompleteLine);
        allAnnounced = complete.Count == DemiatmaWatch.Kinds.Count;
    }

    /// <summary>One line for /helper demiatma.</summary>
    public string Status()
    {
        var counts = Counts();
        return counts is null ? "the inventory is not readable yet." : DemiatmaWatch.StatusLine(counts) + ".";
    }

    private void Say(string line)
    {
        chat.Print("XIV Helper: " + line);
        diag?.Invoke("demiatma: " + line);
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
}
