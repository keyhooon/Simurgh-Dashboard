using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
{
    public POINTL PathSourceSize;
    public RECTL DesktopImageRegion;
    public RECTL DesktopImageClip;
}