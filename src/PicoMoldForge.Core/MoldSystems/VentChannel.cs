namespace PicoMoldForge.Core.MoldSystems;

public sealed record VentChannel(
    string Id,
    decimal StartXmm,
    decimal StartYmm,
    decimal StartZmm,
    decimal EndXmm,
    decimal EndYmm,
    decimal EndZmm,
    decimal WidthMm,
    decimal DepthMm);