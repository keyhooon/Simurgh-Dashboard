namespace SimurghDashboard.Infrastructures.Native;

public enum DISPLAYCONFIG_SCALING : uint
{
    Identity = 1,
    Centered = 2,
    Stretched = 3,
    Aspectratiocenteredmax = 4,
    Custom = 5,
    Preferred = 128,
    ForceUint32 = 0xFFFFFFFF
}