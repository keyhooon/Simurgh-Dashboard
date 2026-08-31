using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SimurghDashboard.Core.Infrastructures.Native;
/*
 * Represents a point-in-time snapshot of the Windows display topology.
 * Storing this structure requires memory $M = (P \times S_p) + (N \times S_m)$
 * where $P$ is the number of paths, $N$ is the number of modes,
 * $S_p$ is sizeof(DISPLAYCONFIG_PATH_INFO), and $S_m$ is sizeof(DISPLAYCONFIG_MODE_INFO).
 */
public static class DisplayConfigHelper
{
    private const uint QDC_ALL_PATHS = 0x00000001;
    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const uint QDC_DATABASE_CURRENT = 0x00000004;
    private const int ERROR_SUCCESS = 0;

    private const int ERROR_INSUFFICIENT_BUFFER = 122;


    // Flags for SetDisplayConfig
    private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
    private const uint SDC_APPLY = 0x00000080;
    private const uint SDC_ALLOW_CHANGES = 0x00000400;
    private const uint SDC_SAVE_TO_DATABASE = 0x00000200;

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME deviceName);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);


    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        out uint currentTopologyId);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int SetDisplayConfig(
        uint numPathArrayElements,
        [In] DISPLAYCONFIG_PATH_INFO[] pathArray,
        uint numModeInfoArrayElements,
        [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        uint flags);


    /// <summary>
    /// Exhaustively queries the Windows CCD (Connecting and Configuring Displays) engine 
    /// to enumerate and trace active graphics adapters, physical ports, monitor descriptors, 
    /// and raster pipeline modes directly into the diagnostic output.
    /// </summary>
    public static void LogActiveDisplaysAndGraphicsPorts()
    {
        Debug.WriteLine("================ [CCD DISPLAY PIPELINE & PORT ENUMERATION] ================");

        // Determine exact buffer allocation sizes for all active pathways and modes
        int queryError = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
        if (queryError != ERROR_SUCCESS)
        {
            Debug.WriteLine($"[CCD API ERROR] Failed to query buffer sizes. Win32 Error: {queryError} ({new Win32Exception(queryError).Message})");
            Debug.WriteLine("============================================================================\n");
            return;
        }

        Debug.WriteLine($"[CCD Buffer Allocation] Paths Array Count: {pathCount} | Modes Array Count: {modeCount}");

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        // Fetch snapshot of active topology
        queryError = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (queryError != ERROR_SUCCESS)
        {
            Debug.WriteLine($"[CCD API ERROR] QueryDisplayConfig failed. Win32 Error: {queryError} ({new Win32Exception(queryError).Message})");
            Debug.WriteLine("============================================================================\n");
            return;
        }

        Debug.WriteLine($"[CCD API] Query successful. Parsing {pathCount} path descriptors...");

        for (int i = 0; i < pathCount; i++)
        {
            var path = paths[i];
            Debug.WriteLine($"\n--- [Pipeline #{i + 1} / {pathCount}] ---");

            // 1. Query GDI Device Name (e.g., \\.\DISPLAY1)
            var sourceDeviceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GetSourceName,
                    size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME)),
                    adapterId = path.sourceInfo.adapterId,
                    id = path.sourceInfo.id
                }
            };

            string gdiDevice = "Unknown (GDI Unavailable)";
            if (DisplayConfigGetDeviceInfo(ref sourceDeviceName) == ERROR_SUCCESS)
            {
                gdiDevice = sourceDeviceName.viewGdiDeviceName;
            }

            // 2. Query GPU Adapter Path and Device Instance
            var adapterName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GetAdapterName,
                    size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME)),
                    adapterId = path.targetInfo.adapterId,
                    id = path.targetInfo.id
                }
            };

            string gpuAdapterPath = "Unknown Adapter Path";
            if (DisplayConfigGetDeviceInfo(ref adapterName) == ERROR_SUCCESS)
            {
                gpuAdapterPath = adapterName.viewGdiDeviceName;
            }

            // 3. Query Physical Monitor Descriptor & EDID Friendly Name
            var targetDeviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GetTargetName,
                    size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME)),
                    adapterId = path.targetInfo.adapterId,
                    id = path.targetInfo.id
                }
            };

            string monitorFriendlyName = "Generic / Virtual Monitor";
            string monitorDevicePath = "Unknown Target Path";
            DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTech = path.targetInfo.outputTechnology;

            if (DisplayConfigGetDeviceInfo(ref targetDeviceName) == ERROR_SUCCESS)
            {
                if (!string.IsNullOrWhiteSpace(targetDeviceName.monitorFriendlyDeviceName))
                {
                    monitorFriendlyName = targetDeviceName.monitorFriendlyDeviceName;
                }
                monitorDevicePath = targetDeviceName.monitorDevicePath;
            }

            // 4. Extract Timing, Refresh Rates & Raster Resolution Modes
            string modeInfoDetails = "Mode Info Index out of bounds";
            if (path.targetInfo.modeInfoIdx < modes.Length)
            {
                var targetMode = modes[path.targetInfo.modeInfoIdx];
                var vSyncNum = targetMode.targetMode.targetVideoSignalInfo.vSyncFreq.Numerator;
                var vSyncDenom = targetMode.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator;
                double refreshRate = vSyncDenom > 0 ? (double)vSyncNum / vSyncDenom : 0.0;

                var activeSize = targetMode.targetMode.targetVideoSignalInfo.activeSize;
                var totalSize = targetMode.targetMode.targetVideoSignalInfo.totalSize;

                modeInfoDetails = $"{activeSize.cx}x{activeSize.cy} (Total: {totalSize.cx}x{totalSize.cy}) @ {refreshRate:F2}Hz (Fraction: {vSyncNum}/{vSyncDenom})";
            }

            // 5. Emit detailed diagnostic traces
            Debug.WriteLine($"  [GDI Binding]         : {gdiDevice}");
            Debug.WriteLine($"  [Monitor Name]        : {monitorFriendlyName}");
            Debug.WriteLine($"  [Physical Connector]  : {outputTech} (Enum: 0x{(int)outputTech:X2})");
            Debug.WriteLine($"  [Hardware Mode]       : {modeInfoDetails}");
            Debug.WriteLine($"  [Rotation/Scaling]    : Rotation={path.targetInfo.rotation}, Scaling={path.targetInfo.scaling}");
            Debug.WriteLine($"  [Pipeline Flags]      : Active={path.flags}, InUse={path.targetInfo.targetAvailable}");
            Debug.WriteLine($"  [GPU LUID]            : High=0x{path.targetInfo.adapterId.HighPart:X8}, Low=0x{path.targetInfo.adapterId.LowPart:X8}");
            Debug.WriteLine($"  [Adapter Hardware ID] : {gpuAdapterPath}");
            Debug.WriteLine($"  [Monitor Hardware ID] : {monitorDevicePath}");
        }

        Debug.WriteLine("============================================================================\n");
    }


    /// <summary>
    /// Generalized discovery method to find the GDI Device Name (e.g., \\.\DISPLAY1) 
    /// matching a specific hardware port technology.
    /// </summary>
    public static string GetMonitorDeviceNameByTechnology(DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY targetTechnology)
    {
        int error = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
        if (error != ERROR_SUCCESS) return null;

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        error = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (error != ERROR_SUCCESS) return null;

        foreach (var path in paths)
        {
            if (path.targetInfo.outputTechnology == targetTechnology)
            {
                var deviceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GetSourceName,
                        size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME)),
                        adapterId = path.sourceInfo.adapterId,
                        id = path.sourceInfo.id
                    }
                };

                if (DisplayConfigGetDeviceInfo(ref deviceName) == ERROR_SUCCESS)
                {
                    return deviceName.viewGdiDeviceName;
                }
            }
        }

        return null;
    }
    /*
             * Queries the CCD (Connecting and Configuring Displays) API to capture the exact
             * physical layout, resolution, and orientation of all active display paths.
             * Time complexity: $O(1)$ native call, though the internal OS execution depends on GPU driver states.
             */
    public static DisplayConfigurationSnapshot CaptureCurrentState()
    {
        uint pathCount, modeCount;
        int result;

        // We use a do-while loop because the display topology might change between 
        // getting the buffer size and actually querying the data (e.g., if a monitor is plugged in).
        do
        {
            result = GetDisplayConfigBufferSizes(QDC_ALL_PATHS, out pathCount, out modeCount);

            if (result != ERROR_SUCCESS)
            {
                throw new Win32Exception(result, "Failed to get display config buffer sizes.");
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            uint topologyId;

            result = QueryDisplayConfig(QDC_ALL_PATHS, ref pathCount, paths, ref modeCount, modes, out topologyId);

            if (result == ERROR_SUCCESS)
            {
                // If the buffer was larger than needed, we resize the arrays to match the exact returned count.
                // This avoids injecting null/empty structs into SetDisplayConfig later.
                Array.Resize(ref paths, (int)pathCount);
                Array.Resize(ref modes, (int)modeCount);

                return new DisplayConfigurationSnapshot
                {
                    Paths = paths,
                    Modes = modes,
                    TopologyId = topologyId
                };
            }

        } while (result == ERROR_INSUFFICIENT_BUFFER); // Retry if the topology changed and buffers became too small

        throw new Win32Exception(result, "Failed to query display configuration.");
    }

    /*
     * Restores the exact hardware display state using the previously captured snapshot.
     * The flags ensure we strictly use the supplied arrays and apply them instantly to the GPU driver.
     */
    public static void ApplyState(DisplayConfigurationSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Paths == null || snapshot.Modes == null)
            return;

        uint flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE;

        int result = SetDisplayConfig(
            (uint)snapshot.Paths.Length,
            snapshot.Paths,
            (uint)snapshot.Modes.Length,
            snapshot.Modes,
            flags);

        if (result != ERROR_SUCCESS)
        {
            // In a crash scenario, throwing an exception here might be swallowed or cause a secondary crash.
            // It is safer to output to debug/trace.
            System.Diagnostics.Trace.WriteLine($"[CCD API] Error applying display state: {new Win32Exception(result).Message}");
        }
    }
    /// <summary>
    /// Retrieves all active monitor outputs with their friendly names and technology types.
    /// Useful for diagnostics or dynamic Kiosk configurations.
    /// </summary>
    public static IEnumerable<(string GdiName, string FriendlyName, DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY Technology)> GetAllActiveDisplays()
    {
        int error = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
        if (error != ERROR_SUCCESS) yield break;

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        error = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (error != ERROR_SUCCESS) yield break;

        foreach (var path in paths)
        {
            var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GetSourceName,
                    size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME)),
                    adapterId = path.sourceInfo.adapterId,
                    id = path.sourceInfo.id
                }
            };

            var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GetTargetName,
                    size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME)),
                    adapterId = path.targetInfo.adapterId,
                    id = path.targetInfo.id
                }
            };

            if (DisplayConfigGetDeviceInfo(ref sourceName) == ERROR_SUCCESS &&
                DisplayConfigGetDeviceInfo(ref targetName) == ERROR_SUCCESS)
            {
                yield return (sourceName.viewGdiDeviceName, targetName.monitorFriendlyDeviceName, path.targetInfo.outputTechnology);
            }
        }
    }
}