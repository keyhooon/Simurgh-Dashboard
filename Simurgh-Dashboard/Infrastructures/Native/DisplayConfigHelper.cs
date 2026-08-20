using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SimurghDashboard.Infrastructures.Native;
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