// ============================================================================
// File: SimurghDashboard.Infrastructures.Native.CCDTypes.cs
// Purpose: Fully expanded Windows CCD (Connecting and Configuring Displays) 
//          API Structures and Enumerations for generalized use.
// ============================================================================

using System;
using System.Runtime.InteropServices;

namespace SimurghDashboard.Infrastructures.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }
}
