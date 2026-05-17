namespace PicoMoldForge.Core.Engineering.Separation;

public sealed record ShutoffStrategyInput(
    IReadOnlyList<ShutoffRegion> Regions,
    bool HasGlobalEngineerOverride = false);