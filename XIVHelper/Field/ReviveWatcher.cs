using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Lumina.Excel.Sheets;
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
    private readonly string raisePrefix;
    private readonly string returnPrefix;
    private readonly string homePointPrefix;
    private readonly IPluginLog log;
    private readonly Action<string>? diag;
    private readonly int returnAfterSeconds;
    private DateTime lastPoll = DateTime.MinValue;
    private DateTime? deadSince;
    private DateTime lastBusy;
    private ulong lastCaster;
    private DateTime lastAsk = DateTime.MinValue;
    private bool returnAnnounced;
    private bool holdAnnounced;
    private DateTime? thanksUntil;
    private string? lastError;

    public ReviveWatcher(IDalamudPluginInterface pluginInterface, IClientState clientState, IObjectTable objects, ICondition condition, IGameGui gameGui,
                         IChatGui chat, IPluginLog log, IDataManager data, Action<string>? diag)
    {
        var addon = data.GetExcelSheet<Addon>();
        raisePrefix = ReviveWatch.Prefix(addon.GetRowOrDefault(ReviveWatch.RaisePromptRow)?.Text.ToString(), ReviveWatch.RaisePrefixFallback);
        returnPrefix = ReviveWatch.Prefix(addon.GetRowOrDefault(ReviveWatch.ReturnPromptRow)?.Text.ToString(), ReviveWatch.ReturnPrefixFallback);
        homePointPrefix = ReviveWatch.Prefix(addon.GetRowOrDefault(ReviveWatch.HomePointPromptRow)?.Text.ToString(), ReviveWatch.ReturnPrefixFallback);
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
            lastBusy = now;
            lastCaster = 0;
            returnAnnounced = false;
            holdAnnounced = false;
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
        var prompt = (AddonSelectYesno*)gameGui.GetAddonByName("SelectYesno").Address;
        var kind = prompt != null && prompt->AtkUnitBase.IsVisible ? Classify(prompt) : PromptKind.None;
        var raiseIncoming = agent->State is ReviveState.Revivable or ReviveState.Reviving || RaiseBeingCast();
        var inActivity = InFateOrCriticalEncounter();
        // the wait counts from the last moment anything was going on, so a cancelled raise or a finished FATE starts it afresh
        if (inActivity || raiseIncoming) lastBusy = now;
        var idleFor = (now - lastBusy).TotalSeconds;
        if (inActivity && (now - since).TotalSeconds >= returnAfterSeconds && !holdAnnounced)
        {
            holdAnnounced = true;
            diag?.Invoke("revive: holding the return until the FATE or CE is over");
        }
        var move = ReviveWatch.Next(true, idleFor, raiseIncoming, kind, inActivity, returnAfterSeconds);
        switch (move)
        {
            case ReviveMove.AcceptRaise:
                diag?.Invoke($"revive: accepting a raise from {agent->ResurrectingPlayerName}");
                prompt->AtkUnitBase.FireCallbackInt(0);
                break;
            case ReviveMove.ConfirmReturn:
                Announce();
                prompt->AtkUnitBase.FireCallbackInt(0);
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

    private unsafe PromptKind Classify(AddonSelectYesno* prompt)
    {
        var text = prompt->PromptText != null ? prompt->PromptText->NodeText.ToString() : null;
        var kind = ReviveWatch.Classify(text, raisePrefix, returnPrefix);
        return kind == PromptKind.Other ? ReviveWatch.Classify(text, raisePrefix, homePointPrefix) : kind;
    }

    private bool RaiseBeingCast()
    {
        var me = objects.LocalPlayer;
        if (me is null) return false;
        var caster = objects.OfType<IBattleChara>().FirstOrDefault(c =>
            c.IsCasting && c.CastTargetObjectId == me.GameObjectId
            && (c.CastActionType == (byte)ActionType.Item ? c.CastActionId == ReviveWatch.PhoenixDown : ReviveWatch.RaiseActions.Contains(c.CastActionId)));
        if (caster is null) return false;
        if (caster.GameObjectId != lastCaster)
        {
            lastCaster = caster.GameObjectId;
            diag?.Invoke($"revive: {caster.Name} is casting a raise");
        }
        return true;
    }

    // the same two tests BOCCHI uses: the FATE the game has the character in, and the dynamic event it is signed up to
    private unsafe bool InFateOrCriticalEncounter()
    {
        var fates = FateManager.Instance();
        if (fates != null && fates->CurrentFate != null) return true;
        var player = objects.LocalPlayer;
        return player != null && ((Character*)player.Address)->EventId.EntryId != 0;
    }

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
