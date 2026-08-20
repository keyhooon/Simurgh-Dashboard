namespace SimurghDashboard.Infrastructures.Native;

public class DisplayConfigurationSnapshot
{
    public DISPLAYCONFIG_PATH_INFO[] Paths { get; set; }
    public DISPLAYCONFIG_MODE_INFO[] Modes { get; set; }
    public uint TopologyId { get; set; }
}