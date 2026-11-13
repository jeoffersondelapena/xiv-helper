using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XIVHelper.HotbarButton;

/// <summary>One hotbar action that shows whether Rotation Solver Reborn is running and flips it between Off and Manual.</summary>
public sealed unsafe class RsrButton : IDisposable
{
    public const uint Toggle = 1_100_001;
    public const uint On = 1_100_002;
    public const uint Off = 1_100_003;
    // real ids the game loads before the texture swap, borrowed from Wrath (violet = on, red = off)
    private const uint ToggleIcon = 1527, OnIcon = 1523, OffIcon = 1526;
    private const string SigReceiveEvent = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 20 48 8B 7C 24 ?? 48 8B D9";
    private const string SigIsActionReplaceable = "40 53 48 83 EC 20 8B D9 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 1F";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IClientState client;
    private readonly IObjectTable objects;
    private readonly IFramework framework;
    private bool? running;
    private readonly IPluginLog log;
    private readonly CustomActionManager manager;
    private readonly Hook<GetAdjustedActionIdDelegate> adjustedHook;
    private readonly Hook<IsActionReplaceableDelegate> replaceableHook;
    private readonly Hook<UseActionDelegate> useActionHook;
    private readonly Hook<AddonActionBarBase.Delegates.ReceiveEvent> receiveEventHook;
    private (int Hotbar, int Slot)? hoveredSlot;
    private bool windowOpen;
    private bool dragging;

    public RsrButton(IDalamudPluginInterface pluginInterface, ISigScanner sig, IGameInteropProvider hooks, ITextureProvider textures,
                     IFramework framework, ICommandManager commands, IClientState client, IObjectTable objects, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.client = client;
        this.objects = objects;
        this.log = log;
        manager = new CustomActionManager(sig, hooks, textures, framework, log);
        var active = pluginInterface.GetIpcSubscriber<bool>("RotationSolverReborn.AutorotationActive");
        RsrState.Probe = () =>
        {
            if (!active.HasFunction)
                return null;   // RSR not loaded yet: asking anyway throws, and that was once per hotbar slot per frame
            try { return active.InvokeFunc(); }
            catch (Exception) { return null; }
        };
        this.framework = framework;
        framework.Update += Refresh;
        var resources = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName!, "Resources");
        manager.Register(new CustomAction(Toggle, "RSR Toggle", "Switches Rotation Solver Reborn between Off and Manual.", ToggleIcon, Path.Combine(resources, "RsrToggle.png")));
        manager.Register(new CustomAction(On, "RSR Manual", "Rotation Solver Reborn is running in Manual mode; press to switch it off.", OnIcon, Path.Combine(resources, "RsrOn.png")));
        manager.Register(new CustomAction(Off, "RSR Off", "Rotation Solver Reborn is off; press to start it in Manual mode.", OffIcon, Path.Combine(resources, "RsrOff.png")));
        adjustedHook = hooks.HookFromAddress<GetAdjustedActionIdDelegate>(ActionManager.Addresses.GetAdjustedActionId.Value, GetAdjustedDetour);
        // the hotbar only asks for an adjusted action (and its icon) when the base action is flagged replaceable, like a combo step
        replaceableHook = hooks.HookFromAddress<IsActionReplaceableDelegate>(sig.ScanText(SigIsActionReplaceable), IsActionReplaceableDetour);
        useActionHook = hooks.HookFromAddress<UseActionDelegate>(ActionManager.Addresses.UseAction.Value, UseActionDetour);
        receiveEventHook = hooks.HookFromAddress<AddonActionBarBase.Delegates.ReceiveEvent>(sig.ScanText(SigReceiveEvent), ReceiveEventDetour);
        adjustedHook.Enable();
        replaceableHook.Enable();
        useActionHook.Enable();
        receiveEventHook.Enable();
        pluginInterface.UiBuilder.Draw += Draw;
    }

    /// <summary>The action a Toggle slot shows: the On icon while RSR runs, the Off icon otherwise.</summary>
    public static uint Shown(bool? running) => running == true ? On : Off;

    public void OpenWindow() => windowOpen = true;

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= Draw;
        framework.Update -= Refresh;
        RsrState.Probe = null;
        adjustedHook.Dispose();
        replaceableHook.Dispose();
        useActionHook.Dispose();
        receiveEventHook.Dispose();
        manager.Dispose();
    }

    private ulong IsActionReplaceableDetour(uint actionId)
        => actionId == Toggle ? 1 : replaceableHook.Original(actionId);

    private void Refresh(IFramework fw) => running = RsrState.Running;

    private uint GetAdjustedDetour(ActionManager* self, uint actionId)
        => actionId == Toggle ? Shown(running) : adjustedHook.Original(self, actionId);

    private bool UseActionDetour(ActionManager* self, ActionType type, uint actionId, ulong targetId, uint extraParam,
                                 ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        if (type != ActionType.Action || !manager.Contains(actionId))
            return useActionHook.Original(self, type, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        Press(actionId);
        return false;
    }

    private void Press(uint actionId)
    {
        var running = RsrState.Running;
        this.running = running;
        if (running is null)
        {
            log.Warning("RSR button pressed, but Rotation Solver Reborn is not loaded");
            return;
        }
        var turningOn = actionId == On || (actionId != Off && !running.Value);
        if (RsrState.WantsHenched(turningOn))
        {
            RsrState.ArmHenched!();
            return;
        }
        var command = actionId == On ? "/rotation manual" : actionId == Off ? "/rotation off" : RsrState.CommandFor(running.Value);
        commands.ProcessCommand(command);
    }

    private void ReceiveEventDetour(AddonActionBarBase* self, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        if (eventType == AtkEventType.DragDropRollOver)
            hoveredSlot = (self->RaptureHotbarId, eventParam);
        else if (eventType == AtkEventType.DragDropRollOut)
            hoveredSlot = null;
        receiveEventHook.Original(self, eventType, eventParam, atkEvent, atkEventData);
    }

    private void Draw()
    {
        if (!windowOpen)
            return;
        if (ImGui.Begin("XIV Helper: RSR button", ref windowOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var state = running switch { true => "Manual (running)", false => "Off", null => "RSR not loaded" };
            ImGui.TextUnformatted($"Rotation Solver Reborn: {state}");
            ImGui.TextUnformatted("Drag this icon onto a hotbar slot. The slot then shows the state and a press flips it.");
            if (manager.IconTextures.TryGetValue(ToggleIcon, out var tex) && tex.TryGetWrap(out var wrap, out _))
            {
                // a button, not a plain image: only an interactive item takes the mouse press, a plain one lets it move the window
                ImGui.PushID("rsrDrag");
                ImGui.ImageButton(wrap.Handle, new Vector2(64, 64));
                ImGui.PopID();
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 2f))
                    dragging = true;
            }
        }
        ImGui.End();

        if (!dragging)
            return;
        if (manager.IconTextures.TryGetValue(ToggleIcon, out var dragTex) && dragTex.TryGetWrap(out var dragWrap, out _))
        {
            ImGui.SetNextWindowPos(ImGui.GetMousePos() - new Vector2(24, 24));
            ImGui.SetNextWindowBgAlpha(0.6f);
            if (ImGui.Begin("##RsrButtonDrag", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs))
                ImGui.Image(dragWrap.Handle, new Vector2(48, 48));
            ImGui.End();
        }
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            dragging = false;
            if (hoveredSlot is { } slot)
                PlaceOnHotbar(slot.Hotbar, slot.Slot);
        }
    }

    private void PlaceOnHotbar(int hotbar, int slot)
    {
        try
        {
            var module = RaptureHotbarModule.Instance();
            module->Hotbars[hotbar].Slots[slot].Set(RaptureHotbarModule.HotbarSlotType.Action, Toggle);
            var copy = module->Hotbars[hotbar].Slots[slot];
            module->WriteSavedSlot(objects.LocalPlayer?.ClassJob.RowId ?? 0, (uint)hotbar, (uint)slot, &copy, false, client.IsPvP);
            log.Information($"RSR button placed on hotbar {hotbar + 1} slot {slot + 1}");
        }
        catch (Exception ex)
        {
            log.Error(ex, "could not place the RSR button");
        }
    }

    private delegate uint GetAdjustedActionIdDelegate(ActionManager* self, uint actionId);
    private delegate ulong IsActionReplaceableDelegate(uint actionId);
    private delegate bool UseActionDelegate(ActionManager* self, ActionType type, uint actionId, ulong targetId, uint extraParam,
                                            ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted);
}
