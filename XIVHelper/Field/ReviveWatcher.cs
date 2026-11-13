using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;
using XIVHelper.Demiatma;

namespace XIVHelper.Field;

/// <summary>While BOCCHI runs in the Occult Crescent: accepts a raise, says thanks for it, and returns to camp after a set
/// time dead with no raise in sight. Frame thread only.</summary>
internal sealed class ReviveWatcher
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IObjectTable objects;
    private readonly ICondition condition;
    private readonly IGameGui gameGui;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Action<string>? diag;
    private readonly int returnAfterSeconds;
    private DateTime lastPoll = DateTime.MinValue;
    private DateTime? deadSince;
    private DateTime lastAsk = DateTime.MinValue;
    private bool returnAnnounced;
    private DateTime? thanksUntil;
    private string? lastError;

    public ReviveWatcher(IDalamudPluginInterface pluginInterface, IClientState clientState, IObjectTable objects, ICondition condition, IGameGui gameGui,
                         IChatGui chat, IPluginLog log, Action<string>? diag)
    {
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.objects = objects;
        this.condition = condition;
        this.gameGui = gameGui;
        this.chat = chat;
        this.log = log;
        this.diag = diag;
        returnAfterSeconds = ReviveWatch.ReturnAfterSeconds(ReadOverride());
    }

    public void Tick(DateTime now)
    {
        if ((now - lastPoll).TotalSeconds < ReviveWatch.PollSeconds) return;
        lastPoll = now;
        if (!clientState.IsLoggedIn || !ReviveWatch.Zones.Contains(clientState.TerritoryType)) return;
        try
        {
            Poll(now);
            lastError = null;
        }
        catch (Exception ex)
        {
            if (lastError == ex.Message) return;
            lastError = ex.Message;
            log.Error(ex, "revive watch failed");
            diag?.Invoke($"revive: FAILED: {ex.GetType().Name}: {ex.Message}");
            chat.PrintError($"XIV Helper: the revive watch stopped working ({ex.Message}); handle raises and returns by hand.");
        }
    }

    private unsafe void Poll(DateTime now)
    {
        var dead = condition[ConditionFlag.Unconscious];
        if (dead && deadSince is null)
        {
            if (BocchiLists.Instance(pluginInterface) is null) return;   // not a BOCCHI run: the player decides
            deadSince = now;
            returnAnnounced = false;
            diag?.Invoke("revive: dead");
        }
        if (!dead && deadSince is not null)
        {
            deadSince = null;
            thanksUntil = now.AddSeconds(3);   // Transcendent lands a moment after a raise; a return never grants it
            diag?.Invoke("revive: alive again");
        }
        if (thanksUntil is { } until)
        {
            if (Raised())
            {
                thanksUntil = null;
                SayThanks();
            }
            else if (now >= until)
            {
                thanksUntil = null;
            }
        }
        if (deadSince is not { } since) return;

        var agent = AgentRevive.Instance();
        var prompt = (AtkUnitBase*)gameGui.GetAddonByName("SelectYesno").Address;
        var promptOpen = prompt != null && prompt->IsVisible;
        var move = ReviveWatch.Next(true, (now - since).TotalSeconds, agent->State != 0, promptOpen, agent->IsAddonShown(), returnAfterSeconds);
        switch (move)
        {
            case ReviveMove.AcceptRaise:
                diag?.Invoke("revive: accepting a raise");
                prompt->FireCallbackInt(0);
                break;
            case ReviveMove.ConfirmReturn:
                Announce();
                prompt->FireCallbackInt(0);
                break;
            case ReviveMove.AskReturn:
                if ((now - lastAsk).TotalSeconds < ReviveWatch.AskAgainSeconds) break;
                lastAsk = now;
                Announce();
                agent->ShowAddon();
                break;
        }
    }

    private void Announce()
    {
        if (returnAnnounced) return;
        returnAnnounced = true;
        var line = ReviveWatch.ReturnLine(returnAfterSeconds);
        chat.Print("XIV Helper: " + line);
        diag?.Invoke("revive: " + line);
    }

    private bool Raised() => objects.LocalPlayer?.StatusList.Any(s => s.StatusId == ReviveWatch.Transcendent) ?? false;

    private unsafe void SayThanks()
    {
        var text = new SeStringBuilder().AddText("/say ").Add(new AutoTranslatePayload(ReviveWatch.ThanksGroup, ReviveWatch.ThanksKey)).Build().Encode();
        var bytes = new byte[text.Length + 1];
        text.CopyTo(bytes, 0);
        fixed (byte* p = bytes)
        {
            var line = Utf8String.FromSequence(p);
            try { RaptureShellModule.Instance()->ExecuteCommandInner(line, UIModule.Instance()); }
            finally { line->Dtor(true); }
        }
        diag?.Invoke("revive: thanked the raiser");
    }

    private string? ReadOverride()
    {
        try
        {
            var path = Path.Combine(pluginInterface.ConfigDirectory.FullName, "revive-return-seconds.txt");
            return File.Exists(path) ? File.ReadLines(path).FirstOrDefault() : null;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "revive override unreadable; using the default wait");
            return null;
        }
    }
}
