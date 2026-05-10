using PicoMoldForge.Core.Parting;

namespace PicoMoldForge.Generator;

public sealed record GeneratorPartingOverride(
    PartingAxis Axis,
    decimal OffsetMm);