namespace SimurghDashboard.Core.Infrastructures.Native;

public enum DISPLAYCONFIG_SCANLINE_ORDERING : uint
{
    Unspecified = 0,
    Progressive = 1,
    Interlaced = 2,
    InterlacedUpperfieldfirst = Interlaced,
    InterlacedLowerfieldfirst = 3,
    ForceUint32 = 0xFFFFFFFF
}