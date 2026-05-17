namespace PicoMoldForge.Core.Engineering.GateSystem;

public sealed record GateRunnerSprueGenerationSummary(
    int SegmentCount,
    int SprueCount,
    int RunnerCount,
    int GateCount,
    int GeneratableSegmentCount,
    int BlockedSegmentCount,
    decimal TotalFlowLengthMm,
    decimal TotalEstimatedVolumeMm3);