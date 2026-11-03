// Ported from Wrath Combo (WrathCombo/Native/CustomActionManager.cs, Puni.sh, BSD 3-Clause; see THIRD-PARTY-NOTICES.md).
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XIVHelper.HotbarButton;

/// <summary>A row the game never shipped: once the Action sheet lookups return it, a hotbar slot takes it like any action.</summary>
public sealed unsafe class CustomAction : IDisposable
{
    public CustomAction(uint id, string name, string description, uint iconId, string iconPath)
    {
        Id = id;
        Name = name;
        Description = description;
        IconId = iconId;
        IconPath = iconPath;

        var nameUtf8 = Encoding.UTF8.GetBytes(Name);
        var rowSize = (nuint)sizeof(CustomActionManager.CustomActionRow);
        ActionRowPtr = (nint)NativeMemory.AllocZeroed(rowSize + (nuint)nameUtf8.Length + 1);
        var row = (CustomActionManager.CustomActionRow*)ActionRowPtr;
        row->NameOffset = (uint)rowSize;
        row->Icon = (ushort)IconId;
        row->ActionCategory = 13;
        row->MaxCharges = 1;
        row->ClassJobCategory = 1;
        row->ClassJob = -1;
        row->CastType = 1;
        row->TargetBools |= 0x08;
        nameUtf8.CopyTo(new Span<byte>((void*)(ActionRowPtr + (nint)rowSize), nameUtf8.Length));
        NamePtr = ActionRowPtr + (nint)rowSize;

        var descBytes = Encoding.UTF8.GetBytes(Description);
        TransientRowPtr = (nint)NativeMemory.AllocZeroed((nuint)(4 + descBytes.Length + 1));
        *(uint*)TransientRowPtr = 4;
        descBytes.CopyTo(new Span<byte>((void*)(TransientRowPtr + 4), descBytes.Length));
    }

    public uint Id { get; }
    public string Name { get; }
    public string Description { get; }
    public uint IconId { get; }
    public string IconPath { get; }
    internal nint ActionRowPtr { get; }
    internal nint TransientRowPtr { get; }
    internal nint NamePtr { get; }

    public void Dispose()
    {
        NativeMemory.Free((void*)ActionRowPtr);
        NativeMemory.Free((void*)TransientRowPtr);
    }
}

public sealed unsafe class CustomActionManager : IDisposable
{
    private const string SigGetActionRow = "48 83 EC 28 48 8B 05 ?? ?? ?? ?? 44 8B C1 BA 04 00 00 00";
    private const string SigGetActionTransient = "E8 ?? ?? ?? ?? 4C 8B E0 48 85 FF 0F 84";
    private const string SigFormatName = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 8B D9 41 8B E9";

    private readonly Dictionary<uint, CustomAction> actions = new();
    private readonly Dictionary<uint, ISharedImmediateTexture> iconTextures = new();
    private readonly List<IconInjectEntry> pendingInjects = new();
    private readonly IFramework framework;
    private readonly ITextureProvider textures;
    private readonly IPluginLog log;

    private readonly Hook<GetActionRowDelegate> getActionRowHook;
    private readonly Hook<RaptureHotbarModule.HotbarSlot.Delegates.IsSlotUsable> isSlotUsableHook;
    private readonly Hook<LoadIconDelegate> loadIconHook;
    private readonly Hook<GetActionRowTransientDelegate> tooltipHook;
    private readonly Hook<FormatNameDelegate> nameHook;

    public CustomActionManager(ISigScanner sig, IGameInteropProvider hooks, ITextureProvider textures, IFramework framework, IPluginLog log)
    {
        this.textures = textures;
        this.framework = framework;
        this.log = log;
        getActionRowHook = hooks.HookFromAddress<GetActionRowDelegate>(sig.ScanText(SigGetActionRow), GetActionRowDetour);
        isSlotUsableHook = hooks.HookFromAddress<RaptureHotbarModule.HotbarSlot.Delegates.IsSlotUsable>(RaptureHotbarModule.HotbarSlot.Addresses.IsSlotUsable.Value, IsSlotUsableDetour);
        loadIconHook = hooks.HookFromAddress<LoadIconDelegate>(AtkComponentIcon.Addresses.LoadIcon.Value, LoadIconDetour);
        tooltipHook = hooks.HookFromAddress<GetActionRowTransientDelegate>(sig.ScanText(SigGetActionTransient), TooltipDetour);
        nameHook = hooks.HookFromAddress<FormatNameDelegate>(sig.ScanText(SigFormatName), FormatNameDetour);
        getActionRowHook.Enable();
        isSlotUsableHook.Enable();
        loadIconHook.Enable();
        tooltipHook.Enable();
        nameHook.Enable();
        framework.Update += OnFrameworkUpdate;
    }

    public IReadOnlyDictionary<uint, ISharedImmediateTexture> IconTextures => iconTextures;

    public bool Contains(uint id) => actions.ContainsKey(id);

    public void Register(CustomAction action)
    {
        actions[action.Id] = action;
        iconTextures[action.IconId] = textures.GetFromFileAbsolute(action.IconPath);
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        var detail = AgentActionDetail.Instance();
        if (detail != null && actions.ContainsKey(detail->ActionId))
        {
            detail->Hide();
            detail->ActionId = 0;
            detail->OriginalId = 0;
            detail->AdjustedId = 0;
        }
        getActionRowHook.Dispose();
        isSlotUsableHook.Dispose();
        loadIconHook.Dispose();
        tooltipHook.Dispose();
        nameHook.Dispose();
        foreach (var action in actions.Values)
            action.Dispose();
        actions.Clear();
        iconTextures.Clear();
        pendingInjects.Clear();
    }

    private byte* FormatNameDetour(int nameType, uint rowId, uint detailType, uint parameter)
        => actions.TryGetValue(rowId, out var act) ? (byte*)act.NamePtr : nameHook.Original(nameType, rowId, detailType, parameter);

    private void* TooltipDetour(uint rowId)
        => actions.TryGetValue(rowId, out var act) ? (void*)act.TransientRowPtr : tooltipHook.Original(rowId);

    private CustomActionRow* GetActionRowDetour(uint rowId)
        => actions.TryGetValue(rowId, out var act) ? (CustomActionRow*)act.ActionRowPtr : getActionRowHook.Original(rowId);

    private bool IsSlotUsableDetour(RaptureHotbarModule.HotbarSlot* self, RaptureHotbarModule.HotbarSlotType type, uint actionId)
        => (type == RaptureHotbarModule.HotbarSlotType.Action && actions.ContainsKey(actionId)) || isSlotUsableHook.Original(self, type, actionId);

    private bool LoadIconDetour(AtkComponentIcon* self, uint iconId)
    {
        var result = loadIconHook.Original(self, iconId);
        if (iconTextures.TryGetValue(iconId, out var tex) && self->Texture != null)
        {
            pendingInjects.RemoveAll(e => e.ComponentPtr == (nint)self);
            pendingInjects.Add(new IconInjectEntry((nint)self, tex, 20));
        }
        return result;
    }

    // the icon component finishes its own load a few frames later; the custom texture goes in once it has
    private void OnFrameworkUpdate(IFramework fw)
    {
        try
        {
            for (var i = pendingInjects.Count - 1; i >= 0; i--)
            {
                var e = pendingInjects[i];
                var icon = (AtkComponentIcon*)e.ComponentPtr;
                if (icon == null)
                    continue;
                var framesLeft = e.FramesLeft - 1;
                if (((uint)icon->Flags & 0x400u) != 0 && framesLeft > 0)
                {
                    pendingInjects[i] = e with { FramesLeft = framesLeft };
                    continue;
                }
                var wrap = e.Tex.GetWrapOrDefault();
                if (wrap == null)
                {
                    pendingInjects[i] = e with { FramesLeft = framesLeft };
                    continue;
                }
                var imgNode = icon->IconImage;
                var partsList = imgNode == null ? null : imgNode->PartsList;
                var part = partsList == null || partsList->PartCount == 0 ? null : partsList->Parts;
                if (part == null)
                {
                    pendingInjects.RemoveAt(i);
                    continue;
                }
                LoadInto(part, wrap);
                loadIconHook.Original(icon, icon->IconId);
                pendingInjects.RemoveAt(i);
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "hotbar icon injection failed");
        }
    }

    // after KamiToolKit, by way of Wrath: the part keeps a kernel texture made from the wrap, which must outlive it
    private void LoadInto(AtkUldPart* part, Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap wrap)
    {
        if (part->UldAsset == null)
            return;
        ref var atkTexture = ref part->UldAsset->AtkTexture;
        if (atkTexture.IsTextureReady() && atkTexture.TextureType != 0 && atkTexture.KernelTexture != null)
        {
            atkTexture.ReleaseTexture();
            atkTexture.KernelTexture = null;
            atkTexture.TextureType = 0;
        }
        var kernel = (Texture*)textures.ConvertToKernelTexture(wrap, true);
        if (kernel == null)
            return;
        atkTexture.KernelTexture = kernel;
        atkTexture.TextureType = TextureType.KernelTexture;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x3F)]
    internal struct CustomActionRow
    {
        [FieldOffset(0x00)] public uint NameOffset;
        [FieldOffset(0x08)] public ushort Icon;
        [FieldOffset(0x0E)] public ushort PrimaryCostValue;
        [FieldOffset(0x14)] public ushort Cast100ms;
        [FieldOffset(0x16)] public ushort Recast100ms;
        [FieldOffset(0x22)] public byte ActionCategory;
        [FieldOffset(0x28)] public byte CastType;
        [FieldOffset(0x29)] public byte EffectRange;
        [FieldOffset(0x2B)] public byte PrimaryCostType;
        [FieldOffset(0x2E)] public byte CooldownGroup;
        [FieldOffset(0x2F)] public byte AdditionalRecastGroup;
        [FieldOffset(0x30)] public byte MaxCharges;
        [FieldOffset(0x33)] public byte ClassJobCategory;
        [FieldOffset(0x37)] public sbyte ClassJob;
        [FieldOffset(0x38)] public sbyte Range;
        [FieldOffset(0x3B)] public byte TargetBools;
    }

    private delegate CustomActionRow* GetActionRowDelegate(uint rowId);
    private delegate bool LoadIconDelegate(AtkComponentIcon* self, uint iconId);
    private delegate void* GetActionRowTransientDelegate(uint rowId);
    private delegate byte* FormatNameDelegate(int nameType, uint rowId, uint detailType, uint parameter);
    private record struct IconInjectEntry(nint ComponentPtr, ISharedImmediateTexture Tex, int FramesLeft);
}
