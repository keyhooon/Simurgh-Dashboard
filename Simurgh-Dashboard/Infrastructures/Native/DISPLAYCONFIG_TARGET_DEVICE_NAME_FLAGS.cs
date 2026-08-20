using System.Runtime.InteropServices;

namespace SimurghDashboard.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS
{
    public uint value;
}