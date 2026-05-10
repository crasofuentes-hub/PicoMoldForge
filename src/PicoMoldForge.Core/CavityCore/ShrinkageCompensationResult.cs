namespace PicoMoldForge.Core.CavityCore;

public sealed record ShrinkageCompensationResult(
    decimal ShrinkageRate,
    decimal ScaleFactor);