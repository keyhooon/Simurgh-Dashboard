// ============================================================================
// File: MonitorHelper.cs
// Purpose: Native Display Management and Layout Control for WPF Kiosk
// Description: Provides granular control over legacy GDI/User32 display APIs.
// This works alongside CCD API to manage rotations, resolutions, and topology.
// ============================================================================

using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native
{
    /*
     * Enum mapping for display orientations according to Windows API.
     * 0 = Default (Landscape), 1 = 90 degrees (Portrait), 
     * 2 = 180 degrees (Landscape Flipped), 3 = 270 degrees (Portrait Flipped).
     */
    public static class MonitorHelper
    {
        #region Native Constants

        /*
         * Native constants for EnumDisplaySettings and ChangeDisplaySettingsEx.
         * The constants dictate how the OS should apply the memory structures to the hardware.
         */
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int DISP_CHANGE_SUCCESSFUL = 0;

        // Updates the registry so the setting persists across reboots.
        private const int CDS_UPDATEREGISTRY = 0x01;
        // Tests the hardware to see if the requested mode is supported by the GPU driver.
        private const int CDS_TEST = 0x02;

        private const uint MONITORINFOF_PRIMARY = 1;

        // Flags to explicitly tell the OS which fields in the DEVMODE struct we are modifying.
        // Failing to set these flags often results in ignored API calls.
        private const int DM_DISPLAYORIENTATION = 0x00000080;
        private const int DM_PELSWIDTH = 0x00080000;
        private const int DM_PELSHEIGHT = 0x00100000;

        #endregion

        #region State Management

        /*
         * Holds cached references to display devices (e.g., DeviceName mapped to ID/Capabilities)
         * to prevent expensive repeated calls to EnumDisplayDevices.
         * Space complexity: $O(D)$ where $D$ is the number of unique display devices connected.
         */
        private static readonly Dictionary<string, object> _deviceCache = new Dictionary<string, object>();
        private static readonly object _cacheLock = new object();

        /*
         * Flushes any in-memory references to hardware states.
         * Time complexity: $O(1)$ amortized for dictionary clear.
         * Essential after a layout reversion so that subsequent calls to Native APIs 
         * do not rely on stale handle data.
         */
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                if (_deviceCache.Count > 0)
                {
                    System.Diagnostics.Trace.WriteLine($"[MonitorHelper] Clearing {_deviceCache.Count} cached monitor handles/states.");
                    _deviceCache.Clear();
                }
            }
        }

        #endregion

        #region Native Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            // Calculated dimensions based on coordinate geometry: $W = x_2 - x_1$, $H = y_2 - y_1$
            public int Width => right - left;
            public int Height => bottom - top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor; // Total display area
            public RECT rcWork;    // Usable display area (excluding taskbar, etc.)
            public uint dwFlags;

            // Fixed size array for the device name (e.g., "\\.\DISPLAY1").
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        // DEVMODE contains exhaustive details about the display device context.
        // Sequential layout is critical to prevent Marshalling exceptions.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields; // Bitmask dictating which following fields are active
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        #endregion

        #region P/Invoke Signatures

        // Delegate for the unmanaged callback. Must be kept alive to avoid GC collection during execution.
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        #endregion

        #region Public Methods

        /*
         * Retrieves bounds and properties of all connected monitors.
         * Time complexity: $O(M)$ where $M$ is the number of active display monitors attached.
         * Uses an unmanaged callback function to iterate over the Windows display device context.
         */
        public static IEnumerable<MONITORINFOEX> GetAllMonitors()
        {
            var monitors = new List<MONITORINFOEX>();

            // Define the callback. In synchronous contexts like this, local functions are safe 
            // as EnumDisplayMonitors blocks until completion, preventing premature GC cleanup.
            bool Callback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
            {
                var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    // Clean up trailing null terminators injected by unmanaged string marshaling
                    mi.szDevice = mi.szDevice?.TrimEnd('\0');
                    monitors.Add(mi);
                }
                return true; // Continue enumeration
            }

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
            return monitors;
        }

        /*
         * Iterates through the list of monitors to find the one designated as primary by the OS.
         * Falls back to an empty RECT if no primary is identified (which is highly unusual in Windows).
         */
        public static RECT GetPrimaryMonitorBounds()
        {
            foreach (var monitor in GetAllMonitors())
            {
                if ((monitor.dwFlags & MONITORINFOF_PRIMARY) == MONITORINFOF_PRIMARY)
                    return monitor.rcMonitor;
            }
            return new RECT();
        }

        /*
         * Looks up a monitor's bounding rectangle by its internal device name (e.g., "\\.\DISPLAY1").
         * This is critical for placing Kiosk windows specifically on secondary screens without 
         * hardcoding coordinates that might change if topology shifts.
         */
        public static RECT GetMonitorBoundsByName(string targetDeviceName)
        {
            if (string.IsNullOrWhiteSpace(targetDeviceName))
                return GetPrimaryMonitorBounds();

            foreach (var monitor in GetAllMonitors())
            {
                if (string.Equals(monitor.szDevice, targetDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return monitor.rcMonitor;
                }
            }

            // General fallback strategy to ensure the application always remains visible.
            System.Diagnostics.Trace.WriteLine($"[MonitorHelper] Device {targetDeviceName} not found. Falling back to primary bounds.");
            return GetPrimaryMonitorBounds();
        }

        /*
         * Queries the exact current display settings for a specific output device.
         * Allocates the required DEVMODE struct size prior to the native call to avoid memory corruption.
         */
        public static DEVMODE GetCurrentSettings(string deviceName)
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode);
            return devMode;
        }

        /*
         * Sets a monitor orientation safely using a two-phase commit model.
         * Phase 1: Test hardware capabilities (CDS_TEST) to prevent black screens/driver crashes.
         * Phase 2: Commit changes to the registry (CDS_UPDATEREGISTRY) for persistence.
         * 
         * Orientation transforms swap dimensions: If rotating by $\pm 90^\circ$, 
         * new Width $W' = H$ and new Height $H' = W$.
         */
        public static bool SetOrientation(string deviceName, DisplayOrientation targetOrientation)
        {
            DEVMODE dm = GetCurrentSettings(deviceName);

            if (dm.dmDisplayOrientation == (int)targetOrientation)
            {
                // Orientation already matches, early exit to avoid unnecessary hardware flickers.
                return true;
            }

            // Determine if the transition crosses the portrait/landscape boundary.
            bool isCurrentPortrait = dm.dmDisplayOrientation == (int)DisplayOrientation.Portrait ||
                                     dm.dmDisplayOrientation == (int)DisplayOrientation.PortraitFlipped;

            bool isTargetPortrait = targetOrientation == DisplayOrientation.Portrait ||
                                    targetOrientation == DisplayOrientation.PortraitFlipped;

            // If moving from Landscape <-> Portrait, we MUST swap the width and height resolution values.
            if (isCurrentPortrait != isTargetPortrait)
            {
                // Tuple swap for efficiency
                (dm.dmPelsWidth, dm.dmPelsHeight) = (dm.dmPelsHeight, dm.dmPelsWidth);
            }

            dm.dmDisplayOrientation = (int)targetOrientation;

            // Crucial step: Inform the OS explicitly which fields have been modified. 
            // If dmFields is not updated, the OS ignores the dimension swaps.
            dm.dmFields = DM_DISPLAYORIENTATION | DM_PELSWIDTH | DM_PELSHEIGHT;

            // Phase 1: Ensure the hardware/GPU actually supports the requested $W \times H$ resolution matrix.
            int testResult = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
            if (testResult != DISP_CHANGE_SUCCESSFUL)
            {
                System.Diagnostics.Trace.WriteLine($"[MonitorHelper] CDS_TEST failed with code {testResult} for {deviceName}. Output rejected by driver.");
                return false;
            }

            // Phase 2: Apply and save to the registry.
            int finalResult = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);

            if (finalResult != DISP_CHANGE_SUCCESSFUL)
            {
                System.Diagnostics.Trace.WriteLine($"[MonitorHelper] CDS_UPDATEREGISTRY failed with code {finalResult} for {deviceName}.");
            }

            return finalResult == DISP_CHANGE_SUCCESSFUL;
        }

        #endregion
    }
}
