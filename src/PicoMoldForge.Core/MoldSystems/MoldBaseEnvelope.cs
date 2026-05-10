namespace PicoMoldForge.Core.MoldSystems;

public sealed record MoldBaseEnvelope(
    decimal WidthMm,
    decimal DepthMm,
    decimal HeightMm,
    decimal MarginMm);