// ============================================================================
// File: SimurghDashboard.Infrastructures.Native.CCDTypes.cs
// Purpose: Fully expanded Windows CCD (Connecting and Configuring Displays) 
//          API Structures and Enumerations for generalized use.
// ============================================================================

using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }
}
