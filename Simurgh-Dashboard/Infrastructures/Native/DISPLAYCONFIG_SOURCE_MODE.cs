using System.Runtime.InteropServices;

namespace SimurghDashboard.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_SOURCE_MODE
{
    public uint width;
    public uint height;
    public DISPLAYCONFIG_PIXELFORMAT pixelFormat;
    public POINTL position;
}