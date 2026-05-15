namespace PicoMoldForge.Core.Engineering.Draft;

public sealed record DraftRuleInput(
    DraftMaterial Material,
    DraftSurfaceType SurfaceType,
    DraftFeatureType FeatureType,
    decimal ActualDraftDeg,
    decimal? TextureDepthMm = null,
    decimal? FeatureDepthMm = null,
    bool IsCosmeticCritical = false,
    bool HasEngineerOverride = false);