using PicoMoldForge.PicoGK.Runtime;
using Xunit;

namespace PicoMoldForge.PicoGK.Tests;

public sealed class PicoGkRuntimeProbeTests
{
    [Fact]
    public void Probe_ReturnsRuntimeMetadata()
    {
        var probe = new PicoGkRuntimeProbe();

        var info = probe.Probe();

        Assert.False(string.IsNullOrWhiteSpace(info.Name));
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
        Assert.False(string.IsNullOrWhiteSpace(info.BuildInfo));
    }
}