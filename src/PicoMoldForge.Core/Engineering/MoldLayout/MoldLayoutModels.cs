using PicoMoldForge.Core.Engineering.GateSystem;
using PicoMoldForge.Core.Engineering.Separation;
using PicoMoldForge.Core.Engineering.WallThickness;

namespace PicoMoldForge.Core.Engineering.MoldLayout;

/// <summary>A point in part coordinates, mm.</summary>
public sealed record MoldLayoutPoint(double X, double Y, double Z);

/// <summary>The mold block, mm.</summary>
public sealed record MoldLayoutBlock(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ);

/// <summary>
/// Rules of thumb of the layout. They are design defaults, not values taken from a standard:
/// <list type="bullet">
/// <item>Gate: diameter <paramref name="GateDiameterToWallRatio"/> x the thickness of the section it enters (at least
/// <paramref name="MinimumGateDiameterMm"/>), land <paramref name="GateLandMm"/>.</item>
/// <item>Runner: diameter = largest wall thickness + <paramref name="RunnerOverWallMm"/>, at least
/// <paramref name="MinimumRunnerDiameterMm"/>. Sprue: runner diameter + <paramref name="SprueOverRunnerMm"/>
/// (a cylinder standing for the tapered sprue).</item>
/// <item>Runner and sprue keep <paramref name="FeedCavityClearanceMm"/> of steel from the cavity; next to the gate the
/// runner is one gate land away from it.</item>
/// <item>Cooling channels, ejector pins and the feed keep <paramref name="FeatureClearanceMm"/> of steel between
/// them.</item>
/// <item>Ejector pins land on faces of the moving half that face its back plate, flat within
/// <paramref name="PinFlatToleranceMm"/>, the pin <paramref name="PinEdgeMarginMm"/> inside the face outline, pins at
/// least <paramref name="PinSpacingToDiameter"/> x their diameter apart (centre to centre).</item>
/// <item>Guide pins: <paramref name="GuidePinCount"/> pins of <paramref name="GuidePinDiameterMm"/> near the corners of the
/// block, standing on the moving half and entering bores of the fixed half by
/// <paramref name="GuidePinProtrusionToDiameter"/> x their diameter.</item>
/// </list>
/// </summary>
public sealed record MoldLayoutOptions(
    double ResolutionMm = 0.1,
    double GateLandMm = 1.0,
    double GateDiameterToWallRatio = 0.6,
    double MinimumGateDiameterMm = 0.5,
    double RunnerOverWallMm = 1.5,
    double MinimumRunnerDiameterMm = 3.0,
    double SprueOverRunnerMm = 1.0,
    double FeedCavityClearanceMm = 0.5,
    double FeatureClearanceMm = 3.0,
    double PinEdgeMarginMm = 0.5,
    double PinSpacingToDiameter = 2.0,
    double PinFlatToleranceMm = 0.1,
    int GateDirectionCount = 16,
    double GuidePinDiameterMm = 8.0,
    double GuidePinProtrusionToDiameter = 1.5,
    int GuidePinCount = 4);

/// <summary>
/// What the layout is placed around: the cavity (the part scaled for shrinkage) and its split, the block, and the
/// configured features. <paramref name="CoolingChannelsPerHalf"/> channels are placed in each half;
/// <paramref name="CoolingCavityClearanceMm"/> is the steel between a channel wall and the cavity;
/// <paramref name="MoldEdgeClearanceMm"/> the steel a channel and the feed keep from the faces of the block they do
/// not pass through.
/// </summary>
public sealed record MoldLayoutRequest(
    IReadOnlyList<MeshTriangle> CavityTriangles,
    PartingAxis Axis,
    double PartingOffsetMm,
    MoldLayoutBlock Block,
    double MaximumWallThicknessMm,
    double EjectorPinDiameterMm,
    int EjectorPinCount,
    double CoolingChannelDiameterMm,
    double CoolingChannelSpacingMm,
    int CoolingChannelsPerHalf,
    double CoolingCavityClearanceMm,
    double MoldEdgeClearanceMm,
    double GuideBoreClearanceMm = 0.5);

/// <summary>
/// The two halves. <paramref name="MovingHalf"/> is +1 for the half on the positive side of the plane (it opens along
/// +axis) or -1; the part stays on it when the mold opens and the ejector pins push it off. Areas are the cavity
/// surface held by each half; grips the shrinkage grip of each half (area x inward distance, mm3, see the planner).
/// </summary>
public sealed record MoldLayoutHalves(
    int MovingHalf,
    string Reason,
    double AreaPositiveMm2,
    double AreaNegativeMm2,
    double GripPositiveMm3,
    double GripNegativeMm3);

/// <summary>
/// One straight piece of the feed system: its centreline from <paramref name="Start"/> to <paramref name="End"/> and
/// its diameter. Clearances are surface to surface, mm: <paramref name="CavityClearanceMm"/> measured from the cavity
/// (for the gate, which enters the cavity by design, see <see cref="MoldFeedLayout"/>);
/// <paramref name="MoldEdgeClearanceMm"/> from the faces of the block (the sprue leaves through the outer face of the
/// fixed half, which is not counted).
/// </summary>
public sealed record MoldFeedSegment(
    string Id,
    GateSystemFeatureType Type,
    MoldLayoutPoint Start,
    MoldLayoutPoint End,
    double DiameterMm,
    double CavityClearanceMm,
    double MoldEdgeClearanceMm);

/// <summary>
/// Sprue, runner and edge gate. The gate lies on the parting plane at <paramref name="GatePoint"/>, on the cavity
/// outline, and enters the cavity against <paramref name="OutwardDirection"/>; the section it enters is
/// <paramref name="EntryThicknessMm"/> thick (the smaller of the part thickness along the axis there and the width of
/// the entry). <paramref name="GateReachesCavity"/>: the inner end of the gate lies inside the cavity;
/// <paramref name="GateClearBeyondEntry"/>: beyond its entry the gate does not meet the cavity again.
/// </summary>
public sealed record MoldFeedLayout(
    bool IsFeasible,
    MoldLayoutPoint GatePoint,
    MoldLayoutPoint OutwardDirection,
    double EntryThicknessMm,
    double EntryWidthMm,
    bool GateReachesCavity,
    bool GateClearBeyondEntry,
    IReadOnlyList<MoldFeedSegment> Segments,
    IReadOnlyList<string> Problems);

/// <summary>
/// One ejector pin: its tip on the part, its other end on the outer face of the moving half. Clearances are surface
/// to surface, mm (+infinity when there is nothing to keep clear of).
/// </summary>
public sealed record MoldEjectorPinLayout(
    string Id,
    MoldLayoutPoint Tip,
    MoldLayoutPoint End,
    double DiameterMm,
    double StrokeMm,
    double SupportedAreaMm2,
    double FeedClearanceMm,
    double CoolingClearanceMm,
    double MoldEdgeClearanceMm);

/// <summary>
/// One straight drilled cooling channel in half <paramref name="Half"/> (+1 or -1), through the block from face to
/// face. Clearances are surface to surface, mm (+infinity when there is nothing to keep clear of);
/// <paramref name="MoldEdgeClearanceMm"/> leaves out the two faces it passes through.
/// </summary>
public sealed record MoldCoolingChannelLayout(
    string Id,
    int Half,
    MoldLayoutPoint Start,
    MoldLayoutPoint End,
    double DiameterMm,
    double CavityClearanceMm,
    double MoldEdgeClearanceMm,
    double PinClearanceMm,
    double FeedClearanceMm);

/// <summary>
/// One guide pin: it stands on the moving half at <paramref name="Base"/> (on the parting plane) and reaches
/// <paramref name="Tip"/> inside the fixed half, which has a bore of <paramref name="BoreDiameterMm"/> from the plane to
/// <paramref name="BoreEnd"/>. <paramref name="ClearanceMm"/> is the smallest steel between the bore and the cavity or
/// any other feature.
/// </summary>
public sealed record MoldGuidePinLayout(
    string Id,
    MoldLayoutPoint Base,
    MoldLayoutPoint Tip,
    double DiameterMm,
    MoldLayoutPoint BoreEnd,
    double BoreDiameterMm,
    double ClearanceMm);

/// <summary>The layout of the mold features around the cavity, with the notes that explain it.</summary>
public sealed record MoldLayoutPlan(
    PartingAxis Axis,
    double PartingOffsetMm,
    MoldLayoutHalves Halves,
    MoldFeedLayout? Feed,
    IReadOnlyList<MoldEjectorPinLayout> Pins,
    int RequestedPinCount,
    double PinSpacingMm,
    IReadOnlyList<MoldCoolingChannelLayout> Channels,
    MoldLayoutOptions Options,
    IReadOnlyList<string> Notes,
    IReadOnlyList<MoldGuidePinLayout> GuidePins);
