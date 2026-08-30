using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct POINTL
{
    public int x;
    public int y;
}