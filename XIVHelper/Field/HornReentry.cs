using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XIVHelper.Field;

/// <summary>Reads South Horn's timer while inside; when the zone changes with the timer at zero, walks to the coordinator,
/// takes the South Horn entry, presses Commence and starts BOCCHI once back in. Frame thread only.</summary>
internal sealed class HornReentry
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IObjectTable objects;
    private readonly ICondition condition;
    private readonly IGameGui gameGui;
    private readonly ICommandManager commands;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Action<string>? diag;
    private readonly Action<string, string?> attention;
    private bool bocchiNoted;
    private bool noted;
    private readonly ICallGateSubscriber<Vector3, bool, bool> moveTo;
    private readonly ICallGateSubscriber<object> stopMoving;
    private readonly string togglePath;
    private bool enabled;
    private bool runSeen;
    private uint wasInHorn;
    private uint target;
    private double? lastTimeLeft;
    private DateTime lastPoll = DateTime.MinValue;
    private DateTime lastAct = DateTime.MinValue;
    private DateTime stepSince;
    private HornStep step = HornStep.Idle;
    private string? lastError;

    public HornReentry(IDalamudPluginInterface pluginInterface, IClientState clientState, IObjectTable objects, ICondition condition, IGameGui gameGui,
                       ICommandManager commands, IChatGui chat, IPluginLog log, Action<string>? diag, Action<string, string?> attention)
    {
        this.attention = attention;
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.objects = objects;
        this.condition = condition;
        this.gameGui = gameGui;
        this.commands = commands;
        this.chat = chat;
        this.log = log;
        this.diag = diag;
        moveTo = pluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        stopMoving = pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
        togglePath = Path.Combine(pluginInterface.ConfigDirectory.FullName, "horn.txt");
        enabled = ReadToggle();
    }

    public bool Enabled => enabled;

    /// <summary>/helper horn [on|off]: shows or sets the switch; the setting survives reloads.</summary>
    public string Command(string argument)
    {
        var arg = argument.Trim().ToLowerInvariant();
        if (arg is "on" or "off")
        {
            enabled = arg == "on";
            if (!enabled) Abort(null);
            WriteToggle();
        }
        return HornWatch.ToggleLine(enabled) + (step == HornStep.Idle ? "" : $" Re-entry in progress: {step}.");
    }

    public void Tick(DateTime now)
    {
        if ((now - lastPoll).TotalSeconds < HornWatch.PollSeconds) return;
        lastPoll = now;
        if (!clientState.IsLoggedIn) return;
        try
        {
            WatchTimer(now);
            if (step != HornStep.Idle) Run(now);
            lastError = null;
        }
        catch (Exception ex)
        {
            if (lastError == ex.Message) return;
            lastError = ex.Message;
            log.Error(ex, "re-entry failed");
            diag?.Invoke($"horn: FAILED: {ex.GetType().Name}: {ex.Message}");
            chat.PrintError($"XIV Helper: re-entry stopped working ({ex.Message}); go back in by hand.");
            Note(HornWatch.BrokenNote(ex.Message, DateTime.Now));
            step = HornStep.Idle;
        }
    }

    private unsafe void WatchTimer(DateTime now)
    {
        var territory = clientState.TerritoryType;
        if (HornWatch.Horns.ContainsKey(territory))
        {
            var director = EventFramework.Instance()->GetPublicContentDirector();
            if (director != null) lastTimeLeft = director->ContentTimeLeft;
            // the automation stops with the kick, so whether a run was on is remembered from inside
            runSeen = OnRun();
            wasInHorn = territory;
            if (noted)
            {
                noted = false;   // back inside, by whatever means: the standing note is done
                attention("Horn", null);
            }
            return;
        }
        if (wasInHorn == 0) return;
        var horn = wasInHorn;
        var name = HornWatch.Horns[horn];
        wasInHorn = 0;
        var timeLeft = lastTimeLeft;
        lastTimeLeft = null;
        diag?.Invoke($"horn: left {name}, timer read {timeLeft?.ToString("0.0") ?? "none"} s");
        if (!enabled || !runSeen) return;
        if (!HornWatch.WasKicked(timeLeft))
        {
            diag?.Invoke("horn: " + HornWatch.LeftLine(name, timeLeft));
            return;
        }
        target = horn;
        Say(HornWatch.KickedLine(name, timeLeft!.Value));
        Enter(HornStep.Armed, now);
    }

    private unsafe void Run(DateTime now)
    {
        if (!enabled || Bocchi.Instance(pluginInterface) is null)
        {
            Abort("BOCCHI is not loaded or re-entry was switched off");
            return;
        }
        if ((now - stepSince).TotalSeconds > HornWatch.TimeoutFor(step))
        {
            Abort(HornWatch.GaveUpLine(step));
            return;
        }
        var loading = condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51];
        switch (step)
        {
            case HornStep.Armed:
                if (loading || !HornWatch.PhantomVillage.Contains(clientState.TerritoryType) || objects.LocalPlayer is null) return;
                Enter(HornStep.Approach, now);
                return;
            case HornStep.Approach:
            {
                var npc = Coordinator();
                var me = objects.LocalPlayer;
                if (npc is null || me is null) return;
                if (Vector3.Distance(npc.Position, me.Position) > HornWatch.InteractRange)
                {
                    if ((now - lastAct).TotalSeconds < 5) return;
                    lastAct = now;
                    diag?.Invoke("horn: walking to the coordinator");
                    moveTo.InvokeFunc(npc.Position, false);
                    return;
                }
                TryStop();
                Enter(HornStep.Interact, now);
                return;
            }
            case HornStep.Interact:
            {
                if (Addon("SelectString") != null) { Enter(HornStep.Menu, now); return; }
                if ((now - lastAct).TotalSeconds < 2) return;
                lastAct = now;
                var npc = Coordinator();
                if (npc is null) return;
                TargetSystem.Instance()->InteractWithObject((GameObject*)npc.Address, false);
                return;
            }
            case HornStep.Menu:
            {
                var menu = (AddonSelectString*)Addon("SelectString");
                if (menu == null) return;
                var entries = new List<string>();
                var popup = &menu->PopupMenu.PopupMenu;
                for (var i = 0; i < popup->EntryCount; i++) entries.Add(popup->EntryNames[i].Value->ToString());
                if (HornWatch.MenuIndex(entries, HornWatch.Horns[target]) is not { } index) { Abort($"the coordinator's menu has no {HornWatch.Horns[target]} entry"); return; }
                menu->AtkUnitBase.FireCallbackInt(index);
                Enter(HornStep.Confirm, now);
                return;
            }
            case HornStep.Confirm:
            {
                var prompt = (AddonSelectYesno*)Addon("SelectYesno");
                if (prompt == null) return;
                var text = prompt->PromptText != null ? prompt->PromptText->NodeText.ToString() : null;
                if (!HornWatch.IsConfirmPrompt(text, HornWatch.Horns[target])) return;
                prompt->AtkUnitBase.FireCallbackInt(0);
                Enter(HornStep.Commence, now);
                return;
            }
            case HornStep.Commence:
            {
                var window = Addon("ContentsFinderConfirm");
                if (window == null) return;
                window->FireCallbackInt(8);
                diag?.Invoke("horn: commenced");
                Enter(HornStep.Entering, now);
                return;
            }
            case HornStep.Entering:
                if (loading || clientState.TerritoryType != target || objects.LocalPlayer is null) return;
                Enter(HornStep.StartBocchi, now);
                return;
            case HornStep.StartBocchi:
                if ((now - stepSince).TotalSeconds < HornWatch.BocchiDelaySeconds) return;
                commands.ProcessCommand(HornWatch.BocchiStart);
                Say(HornWatch.EnteredLine(HornWatch.Horns[target]));
                step = HornStep.Idle;
                return;
        }
    }

    private bool OnRun()
    {
        var on = Bocchi.OnRun(pluginInterface, out var problem);
        if (problem is not null)
        {
            log.Warning($"BOCCHI's run state is out of reach ({problem}); treating a loaded BOCCHI as a run");
            diag?.Invoke($"horn: run state out of reach: {problem}");
            chat.PrintError($"XIV Helper: cannot tell whether BOCCHI is running ({problem}); treating it as running while loaded.");
            attention("BOCCHI", ReviveWatch.BocchiNote(problem, DateTime.Now));
            bocchiNoted = true;
        }
        else if (bocchiNoted && Bocchi.Problem is null)
        {
            bocchiNoted = false;
            attention("BOCCHI", null);
        }
        return on;
    }

    private void Enter(HornStep next, DateTime now)
    {
        step = next;
        stepSince = now;
        lastAct = DateTime.MinValue;
        diag?.Invoke($"horn: {next}");
    }

    private void Abort(string? line)
    {
        if (step == HornStep.Idle) return;
        step = HornStep.Idle;
        TryStop();
        if (line is null) return;
        diag?.Invoke("horn: " + line);
        chat.PrintError("XIV Helper: " + line);
        Note(HornWatch.GaveUpNote(line.TrimEnd('.'), DateTime.Now));
    }

    private void Note(string note)
    {
        noted = true;
        attention("Horn", note);
    }

    private void TryStop()
    {
        try { stopMoving.InvokeAction(); }
        catch (Exception) { }   // vnavmesh not loaded: there was no movement to stop
    }

    private IGameObject? Coordinator() =>
        objects.FirstOrDefault(o => o.BaseId == HornWatch.Jeffroy) ?? objects.FirstOrDefault(o => o.Name.TextValue == "Jeffroy");

    private unsafe AtkUnitBase* Addon(string name)
    {
        var addon = (AtkUnitBase*)gameGui.GetAddonByName(name).Address;
        return addon != null && addon->IsVisible ? addon : null;
    }

    private void Say(string line)
    {
        chat.Print("XIV Helper: " + line);
        diag?.Invoke("horn: " + line);
    }

    private bool ReadToggle()
    {
        try { return File.Exists(togglePath) && HornWatch.ParseToggle(File.ReadLines(togglePath).FirstOrDefault()); }
        catch (Exception ex) { log.Warning(ex, "re-entry switch unreadable; treating it as off"); return false; }
    }

    private void WriteToggle()
    {
        try { File.WriteAllText(togglePath, enabled ? "on\n" : "off\n"); }
        catch (Exception ex) { log.Warning(ex, "re-entry switch not saved; it resets on the next load"); }
    }
}
