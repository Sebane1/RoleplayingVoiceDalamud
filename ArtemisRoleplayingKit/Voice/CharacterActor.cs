using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct CharacterActor {
    [FieldOffset(0x0980)] public Lipsync* Animation;
}
