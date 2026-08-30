using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_TARGET_MODE
{
    public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
}