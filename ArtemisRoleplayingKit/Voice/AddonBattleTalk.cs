using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RoleplayingVoiceDalamud.Voice {

    [StructLayout(LayoutKind.Explicit, Size = 0x298)]
    public unsafe struct AddonBattleTalk {
        [FieldOffset(0x0)] public AtkUnitBase AtkUnitBase;
        [FieldOffset(0x238)] public AtkTextNode* AtkTextNode220;
        [FieldOffset(0x240)] public AtkTextNode* AtkTextNode228;
        [FieldOffset(0x248)] public AtkResNode* AtkResNode230;
        [FieldOffset(0x250)] public AtkNineGridNode* AtkNineGridNode238;
        [FieldOffset(0x258)] public AtkNineGridNode* AtkNineGridNode240;
        [FieldOffset(0x260)] public AtkResNode* AtkResNode248;
        [FieldOffset(0x268)] public AtkImageNode* AtkImageNode250;
        // Shifted +0x18 to match current FFXIVClientStructs layout
        [FieldOffset(0x288)] public AtkResNode* AtkResNode270;
    }
}