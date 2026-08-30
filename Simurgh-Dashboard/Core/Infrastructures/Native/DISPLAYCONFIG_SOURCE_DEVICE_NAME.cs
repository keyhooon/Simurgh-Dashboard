using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string viewGdiDeviceName;
}