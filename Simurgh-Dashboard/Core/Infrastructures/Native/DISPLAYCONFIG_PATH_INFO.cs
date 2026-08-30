using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_INFO
{
    public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
    public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
    public uint flags;
}