namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record MoldSeparationBounds(
    decimal MinXmm,
    decimal MinYmm,
    decimal MinZmm,
    decimal MaxXmm,
    decimal MaxYmm,
    decimal MaxZmm)
{
    public decimal SizeXmm => MaxXmm - MinXmm;

    public decimal SizeYmm => MaxYmm - MinYmm;

    public decimal SizeZmm => MaxZmm - MinZmm;

    public decimal VolumeMm3 => SizeXmm * SizeYmm * SizeZmm;
}