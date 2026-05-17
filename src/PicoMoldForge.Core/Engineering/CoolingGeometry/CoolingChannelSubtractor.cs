using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.CoolingGeometry;

public sealed class CoolingChannelSubtractor
{
    public const string RulePackVersion = "picomoldforge.cooling-channel-subtractor.v1";

    public CoolingChannelSubtractionResult PlanSubtraction(CoolingChannelSubtractionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateInput(input);

        var channelResults = input.Channels
            .Select(channel => EvaluateChannel(channel, input))
            .ToArray();

        var summary = new CoolingChannelSubtractionSummary(
            ChannelCount: channelResults.Length,
            SubtractableChannelCount: channelResults.Count(channel => channel.IsSubtractable),
            BlockedChannelCount: channelResults.Count(channel => !channel.IsSubtractable),
            TotalEstimatedRemovedVolumeMm3: channelResults.Sum(channel => channel.EstimatedRemovedVolumeMm3));

        var ruleResult = BuildRuleResult(channelResults, summary, input);

        return new CoolingChannelSubtractionResult(channelResults, summary, ruleResult);
    }

    private static void ValidateInput(CoolingChannelSubtractionInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Channels);

        if (input.MoldBounds.SizeXmm <= 0m ||
            input.MoldBounds.SizeYmm <= 0m ||
            input.MoldBounds.SizeZmm <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.MoldBounds), "Mold bounds must have positive X, Y, and Z sizes.");
        }

        if (input.RequiredCavityClearanceMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.RequiredCavityClearanceMm), "Required cavity clearance cannot be negative.");
        }

        if (input.RequiredMoldEdgeClearanceMm < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(input.RequiredMoldEdgeClearanceMm), "Required mold-edge clearance cannot be negative.");
        }
    }

    private static CoolingChannelSubtractionChannelResult EvaluateChannel(
        CoolingChannelSegment channel,
        CoolingChannelSubtractionInput input)
    {
        var length = Distance(channel.Start, channel.End);
        var radius = channel.DiameterMm / 2m;
        var estimatedVolume = length <= 0m || channel.DiameterMm <= 0m
            ? 0m
            : 3.1415926535897932384626433833m * radius * radius * length;

        var isInsideMoldBounds =
            IsPointInsideBounds(channel.Start, input.MoldBounds) &&
            IsPointInsideBounds(channel.End, input.MoldBounds);

        var hasCavityClearance = channel.MinimumCavityClearanceMm >= input.RequiredCavityClearanceMm;
        var hasMoldEdgeClearance = channel.MinimumMoldEdgeClearanceMm >= input.RequiredMoldEdgeClearanceMm;

        var isSubtractable =
            !string.IsNullOrWhiteSpace(channel.ChannelId) &&
            length > 0m &&
            channel.DiameterMm > 0m &&
            isInsideMoldBounds &&
            hasCavityClearance &&
            hasMoldEdgeClearance;

        return new CoolingChannelSubtractionChannelResult(
            ChannelId: string.IsNullOrWhiteSpace(channel.ChannelId) ? "missing" : channel.ChannelId,
            LengthMm: Math.Round(length, 6),
            DiameterMm: Math.Max(0m, channel.DiameterMm),
            EstimatedRemovedVolumeMm3: Math.Round(estimatedVolume, 6),
            IsInsideMoldBounds: isInsideMoldBounds,
            HasRequiredCavityClearance: hasCavityClearance,
            HasRequiredMoldEdgeClearance: hasMoldEdgeClearance,
            IsSubtractable: isSubtractable);
    }

    private static EngineeringRuleResult BuildRuleResult(
        IReadOnlyList<CoolingChannelSubtractionChannelResult> channels,
        CoolingChannelSubtractionSummary summary,
        CoolingChannelSubtractionInput input)
    {
        var issues = new List<EngineeringIssue>();

        if (input.HasEngineerOverride)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "cooling-subtraction.override",
                category: "CoolingChannelSubtraction",
                message: "Cooling channel subtraction has an engineer override and requires documented review.",
                correctiveAction: "Document the engineer-approved cooling channel subtraction rationale.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoolingChannels",
                material: null));
        }

        if (channels.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: "cooling-subtraction.channels.missing",
                category: "CoolingChannelSubtraction",
                message: "No cooling channels were provided for subtraction planning.",
                correctiveAction: "Define cooling channels before treating the mold alpha as cooling-capable.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoolingChannels",
                material: null));

            return new EngineeringRuleResult(RulePackVersion, "CoolingChannelSubtraction", issues);
        }

        foreach (var channel in channels)
        {
            AddChannelIssue(channel, input, issues);
        }

        if (issues.Count == 0)
        {
            issues.Add(EngineeringIssueFactory.Pass(
                ruleId: "cooling-subtraction.pass",
                category: "CoolingChannelSubtraction",
                message: "All cooling channels are eligible for preliminary subtraction.",
                correctiveAction: "No action required beyond normal mold-engineering review.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoolingChannels",
                material: null,
                actualValue: summary.TotalEstimatedRemovedVolumeMm3,
                requiredValue: 0m,
                recommendedValue: null,
                unit: "mm3"));
        }

        return new EngineeringRuleResult(RulePackVersion, "CoolingChannelSubtraction", issues);
    }

    private static void AddChannelIssue(
        CoolingChannelSubtractionChannelResult channel,
        CoolingChannelSubtractionInput input,
        List<EngineeringIssue> issues)
    {
        var ruleIdBase = $"cooling-subtraction.{NormalizeRuleId(channel.ChannelId)}";

        if (channel.ChannelId == "missing")
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.id-missing",
                category: "CoolingChannelSubtraction",
                message: "Cooling channel is missing a stable ChannelId.",
                correctiveAction: "Assign a stable ChannelId before subtraction planning.",
                sourceRulePackVersion: RulePackVersion,
                featureType: "CoolingChannel",
                material: null));
        }

        if (channel.LengthMm <= 0m || channel.DiameterMm <= 0m)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.invalid-geometry",
                category: "CoolingChannelSubtraction",
                message: "Cooling channel has invalid length or diameter.",
                correctiveAction: "Provide non-zero channel length and positive channel diameter.",
                sourceRulePackVersion: RulePackVersion,
                featureType: channel.ChannelId,
                material: null,
                actualValue: channel.DiameterMm,
                requiredValue: 0.01m,
                recommendedValue: null,
                unit: "mm"));
        }

        if (!channel.IsInsideMoldBounds)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.outside-mold-bounds",
                category: "CoolingChannelSubtraction",
                message: "Cooling channel endpoint is outside mold bounds.",
                correctiveAction: "Move the cooling channel inside the mold block before subtraction.",
                sourceRulePackVersion: RulePackVersion,
                featureType: channel.ChannelId,
                material: null));
        }

        if (!channel.HasRequiredCavityClearance)
        {
            issues.Add(EngineeringIssueFactory.Fail(
                ruleId: $"{ruleIdBase}.cavity-clearance",
                category: "CoolingChannelSubtraction",
                message: "Cooling channel does not satisfy required cavity clearance.",
                correctiveAction: "Increase channel-to-cavity distance or reduce channel diameter.",
                sourceRulePackVersion: RulePackVersion,
                featureType: channel.ChannelId,
                material: null,
                requiredValue: input.RequiredCavityClearanceMm,
                recommendedValue: input.RequiredCavityClearanceMm,
                unit: "mm"));
        }

        if (!channel.HasRequiredMoldEdgeClearance)
        {
            issues.Add(EngineeringIssueFactory.Warning(
                ruleId: $"{ruleIdBase}.mold-edge-clearance",
                category: "CoolingChannelSubtraction",
                message: "Cooling channel does not satisfy required mold-edge clearance.",
                correctiveAction: "Move the channel farther from mold edges or review mold strength.",
                sourceRulePackVersion: RulePackVersion,
                featureType: channel.ChannelId,
                material: null,
                requiredValue: input.RequiredMoldEdgeClearanceMm,
                recommendedValue: input.RequiredMoldEdgeClearanceMm,
                unit: "mm",
                requiresEngineerReview: true));
        }
    }

    private static bool IsPointInsideBounds(CoolingChannelPoint point, CoolingMoldBounds bounds)
    {
        return point.Xmm >= bounds.MinXmm &&
            point.Xmm <= bounds.MaxXmm &&
            point.Ymm >= bounds.MinYmm &&
            point.Ymm <= bounds.MaxYmm &&
            point.Zmm >= bounds.MinZmm &&
            point.Zmm <= bounds.MaxZmm;
    }

    private static decimal Distance(CoolingChannelPoint a, CoolingChannelPoint b)
    {
        var dx = a.Xmm - b.Xmm;
        var dy = a.Ymm - b.Ymm;
        var dz = a.Zmm - b.Zmm;

        return (decimal)Math.Sqrt((double)((dx * dx) + (dy * dy) + (dz * dz)));
    }

    private static string NormalizeRuleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "missing";
        }

        return value.Trim().Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant();
    }
}