using System.Runtime.InteropServices;

namespace SimurghDashboard.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}