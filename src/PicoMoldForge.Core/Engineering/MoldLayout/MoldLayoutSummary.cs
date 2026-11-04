using PicoMoldForge.Core.Engineering.GateSystem;

namespace PicoMoldForge.Core.Engineering.MoldLayout;

/// <summary>
/// One feature of the mold layout as written to the report: its centreline and diameter, mm. <paramref name="Half"/>
/// is the half it is cut into (+1, -1), 0 for the gate and the runner, which lie on the parting plane.
/// </summary>
public sealed record MoldLayoutFeatureSummary(
    string Id,
    string Kind,
    int Half,
    MoldLayoutPoint Start,
    MoldLayoutPoint End,
    decimal DiameterMm);

/// <summary>
/// The mold layout in the report: which half moves (it keeps the part and carries the ejector pins), whether the feed
/// works, how many pins and channels were placed, the smallest clearances and every feature.
/// </summary>
public sealed record MoldLayoutSummary(
    string MovingHalf,
    string MovingHalfReason,
    bool FeedIsFeasible,
    decimal? GateEntryThicknessMm,
    int EjectorPinCount,
    int RequestedEjectorPinCount,
    int CoolingChannelCount,
    int GuidePinCount,
    decimal? MinimumFeedCavityClearanceMm,
    decimal? MinimumCoolingCavityClearanceMm,
    IReadOnlyList<MoldLayoutFeatureSummary> Features)
{
    public static MoldLayoutSummary From(MoldLayoutPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var axis = plan.Axis.ToString();
        var features = new List<MoldLayoutFeatureSummary>();
        var fixedHalf = -plan.Halves.MovingHalf;

        if (plan.Feed is not null)
        {
            foreach (var segment in plan.Feed.Segments)
            {
                features.Add(new MoldLayoutFeatureSummary(
                    segment.Id,
                    segment.Type.ToString(),
                    segment.Type == GateSystemFeatureType.Sprue ? fixedHalf : 0,
                    Rounded(segment.Start),
                    Rounded(segment.End),
                    Round(segment.DiameterMm)));
            }
        }

        foreach (var pin in plan.Pins)
        {
            features.Add(new MoldLayoutFeatureSummary(pin.Id, "EjectorPin", plan.Halves.MovingHalf, Rounded(pin.Tip), Rounded(pin.End), Round(pin.DiameterMm)));
        }

        foreach (var channel in plan.Channels)
        {
            features.Add(new MoldLayoutFeatureSummary(channel.Id, "CoolingChannel", channel.Half, Rounded(channel.Start), Rounded(channel.End), Round(channel.DiameterMm)));
        }

        foreach (var guide in plan.GuidePins)
        {
            features.Add(new MoldLayoutFeatureSummary(guide.Id, "GuidePin", plan.Halves.MovingHalf, Rounded(guide.Base), Rounded(guide.Tip), Round(guide.DiameterMm)));
            features.Add(new MoldLayoutFeatureSummary(guide.Id.Replace("pin", "bore", StringComparison.Ordinal), "GuideBore", fixedHalf, Rounded(guide.Base), Rounded(guide.BoreEnd), Round(guide.BoreDiameterMm)));
        }

        var feedClearances = plan.Feed?.Segments
            .Where(s => s.Type != GateSystemFeatureType.Gate)
            .Select(s => s.CavityClearanceMm)
            .ToArray() ?? Array.Empty<double>();

        return new MoldLayoutSummary(
            (plan.Halves.MovingHalf > 0 ? "+" : "-") + axis,
            plan.Halves.Reason,
            plan.Feed?.IsFeasible ?? false,
            plan.Feed is null ? null : Round(plan.Feed.EntryThicknessMm),
            plan.Pins.Count,
            plan.RequestedPinCount,
            plan.Channels.Count,
            plan.GuidePins.Count,
            feedClearances.Length == 0 ? null : Round(feedClearances.Min()),
            plan.Channels.Count == 0 ? null : Round(plan.Channels.Min(c => c.CavityClearanceMm)),
            features);
    }

    private static decimal Round(double value) => double.IsFinite(value) ? Math.Round((decimal)value, 4) : 0m;

    private static MoldLayoutPoint Rounded(MoldLayoutPoint p) => new(Math.Round(p.X, 4), Math.Round(p.Y, 4), Math.Round(p.Z, 4));
}
