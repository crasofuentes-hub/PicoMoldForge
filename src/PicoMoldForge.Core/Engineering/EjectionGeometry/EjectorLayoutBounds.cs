namespace PicoMoldForge.Core.Engineering.EjectionGeometry;

public sealed record EjectorLayoutBounds(
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
}