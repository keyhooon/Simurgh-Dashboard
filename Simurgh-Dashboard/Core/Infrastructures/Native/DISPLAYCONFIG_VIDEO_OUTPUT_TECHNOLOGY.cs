namespace SimurghDashboard.Core.Infrastructures.Native;

/// <summary>
/// Represents the video output technology of a display target.
/// Expanded to include all possible Windows CCD technologies for total generalization.
/// </summary>
public enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
{
    Other = 4294967295, // -1
    Hd15 = 0,
    Svideo = 1,
    CompositeVideo = 2,
    ComponentVideo = 3,
    Dvi = 4,
    Hdmi = 5,
    Lvds = 6,
    D_jpn = 8,
    Sdi = 9,
    DisplayportExternal = 10,
    DisplayportEmbedded = 11,
    UdiExternal = 12,
    UdiEmbedded = 13,
    Sdtvdongle = 14,
    Miracast = 15,
    IndirectWired = 16,
    IndirectVirtual = 17,
    Internal = 0x80000000,
    ForceUint32 = 0xFFFFFFFF
}