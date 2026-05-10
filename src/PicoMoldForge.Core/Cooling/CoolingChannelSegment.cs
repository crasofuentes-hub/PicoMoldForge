namespace PicoMoldForge.Core.Cooling;

public sealed record CoolingChannelSegment(
    string Id,
    decimal StartXmm,
    decimal StartYmm,
    decimal StartZmm,
    decimal EndXmm,
    decimal EndYmm,
    decimal EndZmm,
    decimal DiameterMm,
    string Description);