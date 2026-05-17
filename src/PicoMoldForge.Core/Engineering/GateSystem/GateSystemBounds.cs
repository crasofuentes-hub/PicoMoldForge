namespace PicoMoldForge.Core.Engineering.GateSystem;

public sealed record GateSystemBounds(
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