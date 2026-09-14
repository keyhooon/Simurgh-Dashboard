using System.Runtime.InteropServices;

namespace Simurgh.Dashboard.Core.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_2DREGION
{
    public uint cx;
    public uint cy;
}