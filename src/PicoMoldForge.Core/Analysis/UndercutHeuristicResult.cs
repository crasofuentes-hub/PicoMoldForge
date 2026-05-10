namespace PicoMoldForge.Core.Analysis;

public sealed record UndercutHeuristicResult(
    OpeningDirection3 OpeningDirection,
    int TotalTriangleCount,
    int OpposingNormalTriangleCount,
    float OpposingNormalRatio,
    bool HasPotentialUndercutRisk,
    string Method,
    string Limitation);