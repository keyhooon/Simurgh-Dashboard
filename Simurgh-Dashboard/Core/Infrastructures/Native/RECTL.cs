using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RECTL
{
    public int left;
    public int top;
    public int right;
    public int bottom;
}