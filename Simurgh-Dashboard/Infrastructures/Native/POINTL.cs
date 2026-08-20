using System.Runtime.InteropServices;

namespace SimurghDashboard.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct POINTL
{
    public int x;
    public int y;
}