using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}