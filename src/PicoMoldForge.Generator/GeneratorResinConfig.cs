namespace PicoMoldForge.Generator;

/// <summary>
/// The resin the part is molded in (optional "resin" section). Its shrinkage sizes the cavity; without a rate, a
/// known resin uses its recommended rate.
/// </summary>
public sealed record GeneratorResinConfig(
    string Name,
    decimal? ShrinkageRate);
