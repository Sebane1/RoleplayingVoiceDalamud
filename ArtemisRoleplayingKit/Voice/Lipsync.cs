
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct Lipsync {
    [FieldOffset(0x1E2)] public byte SpeedTrigger;
    [FieldOffset(0x2D0)] public ushort BaseOverride;
    [FieldOffset(0x2D2)] public ushort LipsOverride;
}
