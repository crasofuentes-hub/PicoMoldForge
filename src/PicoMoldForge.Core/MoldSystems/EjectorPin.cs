namespace PicoMoldForge.Core.MoldSystems;

public sealed record EjectorPin(
    string Id,
    decimal CenterXmm,
    decimal CenterYmm,
    decimal StartZmm,
    decimal EndZmm,
    decimal DiameterMm);