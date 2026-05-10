using PicoGK;

namespace PicoMoldForge.PicoGK.Runtime;

public sealed class PicoGkRuntimeProbe
{
    public PicoGkRuntimeInfo Probe()
    {
        return new PicoGkRuntimeInfo(
            Library.strName(),
            Library.strVersion(),
            Library.strBuildInfo());
    }
}