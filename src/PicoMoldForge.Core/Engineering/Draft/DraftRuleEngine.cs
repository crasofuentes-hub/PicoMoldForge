using PicoMoldForge.Core.Engineering;

namespace PicoMoldForge.Core.Engineering.Draft;

public sealed class DraftRuleEngine
{
    public const string RulePackVersion = "expert-injection-mold-rules.v1";

    public EngineeringRuleResult Evaluate(DraftRuleInput input)
    {
        var requiredDraftDeg = ResolveRequiredDraftDeg(input);
        var recommendedDraftDeg = ResolveRecommendedDraftDeg(input, requiredDraftDeg);
        var failBelowDeg = ResolveFailBelowDeg(input);

        var issue = EvaluateIssue(
            input,
            requiredDraftDeg,
            recommendedDraftDeg,
            failBelowDeg);

        return new EngineeringRuleResult(
            RulePackVersion: RulePackVersion,
            Category: "Draft",
            Issues: new[] { issue });
    }

    public decimal ResolveRequiredDraftDeg(DraftRuleInput input)
    {
        var featureMinimumDeg = ResolveFeatureMinimumDraftDeg(input.FeatureType);
        var baseDraftDeg = input.FeatureType == DraftFeatureType.Wall
            ? ResolveBaseMaterialDraftDeg(input.Material, input.SurfaceType)
            : featureMinimumDeg;

        var textureIncrementDeg = ResolveTextureIncrementDeg(input.TextureDepthMm);
        var depthIncrementDeg = ResolveDepthIncrementDeg(input.FeatureDepthMm);

        var required = baseDraftDeg + textureIncrementDeg + depthIncrementDeg;

        if (input.IsCosmeticCritical)
        {
            required += 0.5m;
        }

        required = Math.Max(required, featureMinimumDeg);

        if (input.FeatureType == DraftFeatureType.Shutoff)
        {
            required = Math.Max(required, 3.0m);
        }

        return required;
    }

    public decimal ResolveRecommendedDraftDeg(DraftRuleInput input, decimal requiredDraftDeg)
    {
        var recommended = input.FeatureType == DraftFeatureType.Wall
            ? ResolveBaseRecommendedDraftDeg(input.Material, input.SurfaceType)
            : ResolveFeatureRecommendedDraftDeg(input.FeatureType);

        recommended = Math.Max(recommended, requiredDraftDeg);

        return recommended;
    }

    private static EngineeringIssue EvaluateIssue(
        DraftRuleInput input,
        decimal requiredDraftDeg,
        decimal recommendedDraftDeg,
        decimal failBelowDeg)
    {
        var ruleId = BuildRuleId(input);
        var material = input.Material.ToString();
        var featureType = input.FeatureType.ToString();

        if (input.HasEngineerOverride)
        {
            return EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"{ruleId}.override",
                category: "Draft",
                message: "Draft rule has an explicit engineer override and requires documented review.",
                correctiveAction: "Verify and document the engineer-approved override rationale.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualDraftDeg,
                requiredValue: requiredDraftDeg,
                recommendedValue: recommendedDraftDeg,
                unit: "deg");
        }

        if (input.TextureDepthMm is > 0.10m &&
            input.ActualDraftDeg >= requiredDraftDeg)
        {
            return EngineeringIssueFactory.NeedsEngineerReview(
                ruleId: $"{ruleId}.texture.manual-review",
                category: "Draft",
                message: "Texture depth is above 0.10 mm and requires manual engineering review even if draft is otherwise acceptable.",
                correctiveAction: "Request mold engineer review for texture-specific draft requirements.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.TextureDepthMm,
                requiredValue: 0.10m,
                recommendedValue: null,
                unit: "mm");
        }

        if (input.ActualDraftDeg <= 0m &&
            IsStandardMoldReleaseFeature(input.FeatureType))
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.zero-or-negative",
                category: "Draft",
                message: "Draft is zero or negative on a standard mold-release feature.",
                correctiveAction: "Increase draft angle or provide an engineer-approved special release mechanism.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualDraftDeg,
                requiredValue: requiredDraftDeg,
                recommendedValue: recommendedDraftDeg,
                unit: "deg");
        }

        if (input.ActualDraftDeg < failBelowDeg)
        {
            return EngineeringIssueFactory.Fail(
                ruleId: $"{ruleId}.below-fail-threshold",
                category: "Draft",
                message: "Draft is below the expert fail threshold.",
                correctiveAction: "Increase draft angle to meet the minimum engineering threshold.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualDraftDeg,
                requiredValue: requiredDraftDeg,
                recommendedValue: recommendedDraftDeg,
                unit: "deg");
        }

        if (input.ActualDraftDeg < requiredDraftDeg)
        {
            return EngineeringIssueFactory.Warning(
                ruleId: $"{ruleId}.below-required",
                category: "Draft",
                message: "Draft is below the expert required value.",
                correctiveAction: "Increase draft angle or document an engineer-approved exception.",
                sourceRulePackVersion: RulePackVersion,
                featureType: featureType,
                material: material,
                actualValue: input.ActualDraftDeg,
                requiredValue: requiredDraftDeg,
                recommendedValue: recommendedDraftDeg,
                unit: "deg",
                requiresEngineerReview: input.IsCosmeticCritical || input.SurfaceType == DraftSurfaceType.TexturedHeavy);
        }

        return EngineeringIssueFactory.Pass(
            ruleId: $"{ruleId}.pass",
            category: "Draft",
            message: "Draft satisfies the expert rule.",
            correctiveAction: "No action required.",
            sourceRulePackVersion: RulePackVersion,
            featureType: featureType,
            material: material,
            actualValue: input.ActualDraftDeg,
            requiredValue: requiredDraftDeg,
            recommendedValue: recommendedDraftDeg,
            unit: "deg");
    }

    private static decimal ResolveBaseMaterialDraftDeg(
        DraftMaterial material,
        DraftSurfaceType surfaceType)
    {
        return material switch
        {
            DraftMaterial.Abs => surfaceType switch
            {
                DraftSurfaceType.Smooth => 1.0m,
                DraftSurfaceType.TexturedLight => 3.0m,
                DraftSurfaceType.TexturedHeavy => 5.0m,
                _ => 1.0m
            },
            DraftMaterial.Pp => surfaceType switch
            {
                DraftSurfaceType.Smooth => 1.0m,
                DraftSurfaceType.TexturedLight => 3.0m,
                DraftSurfaceType.TexturedHeavy => 5.0m,
                _ => 1.0m
            },
            DraftMaterial.Pc => surfaceType switch
            {
                DraftSurfaceType.Smooth => 1.5m,
                DraftSurfaceType.TexturedLight => 3.0m,
                DraftSurfaceType.TexturedHeavy => 5.0m,
                _ => 1.5m
            },
            DraftMaterial.NylonPa => surfaceType switch
            {
                DraftSurfaceType.Smooth => 0.5m,
                DraftSurfaceType.TexturedLight => 2.5m,
                DraftSurfaceType.TexturedHeavy => 4.5m,
                _ => 0.5m
            },
            DraftMaterial.Pom => ResolveGenericLowFrictionMaterialDraft(surfaceType),
            DraftMaterial.Pe => ResolveGenericLowFrictionMaterialDraft(surfaceType),
            DraftMaterial.Pvc => ResolveGenericLowFrictionMaterialDraft(surfaceType),
            DraftMaterial.Peek => surfaceType switch
            {
                DraftSurfaceType.Smooth => 1.0m,
                DraftSurfaceType.TexturedLight => 3.0m,
                DraftSurfaceType.TexturedHeavy => 5.0m,
                _ => 1.0m
            },
            _ => surfaceType switch
            {
                DraftSurfaceType.Smooth => 1.0m,
                DraftSurfaceType.TexturedLight => 3.0m,
                DraftSurfaceType.TexturedHeavy => 5.0m,
                _ => 1.0m
            }
        };
    }

    private static decimal ResolveBaseRecommendedDraftDeg(
        DraftMaterial material,
        DraftSurfaceType surfaceType)
    {
        return material switch
        {
            DraftMaterial.Pc when surfaceType == DraftSurfaceType.Smooth => 2.0m,
            DraftMaterial.Pc when surfaceType == DraftSurfaceType.TexturedLight => 3.5m,
            DraftMaterial.NylonPa when surfaceType == DraftSurfaceType.Smooth => 1.0m,
            DraftMaterial.NylonPa when surfaceType == DraftSurfaceType.TexturedLight => 3.0m,
            DraftMaterial.NylonPa when surfaceType == DraftSurfaceType.TexturedHeavy => 5.0m,
            DraftMaterial.Pom when surfaceType == DraftSurfaceType.Smooth => 1.0m,
            DraftMaterial.Pe when surfaceType == DraftSurfaceType.Smooth => 1.0m,
            DraftMaterial.Pvc when surfaceType == DraftSurfaceType.Smooth => 1.0m,
            DraftMaterial.Peek when surfaceType == DraftSurfaceType.Smooth => 1.5m,
            _ => surfaceType switch
            {
                DraftSurfaceType.Smooth => 1.5m,
                DraftSurfaceType.TexturedLight => 3.0m,
                DraftSurfaceType.TexturedHeavy => 5.0m,
                _ => 1.5m
            }
        };
    }

    private static decimal ResolveGenericLowFrictionMaterialDraft(DraftSurfaceType surfaceType)
    {
        return surfaceType switch
        {
            DraftSurfaceType.Smooth => 0.5m,
            DraftSurfaceType.TexturedLight => 3.0m,
            DraftSurfaceType.TexturedHeavy => 5.0m,
            _ => 0.5m
        };
    }

    private static decimal ResolveTextureIncrementDeg(decimal? textureDepthMm)
    {
        if (textureDepthMm is null or <= 0m)
        {
            return 0m;
        }

        return textureDepthMm.Value switch
        {
            <= 0.03m => 0.5m,
            <= 0.06m => 1.0m,
            <= 0.10m => 1.5m,
            _ => 2.0m
        };
    }

    private static decimal ResolveDepthIncrementDeg(decimal? featureDepthMm)
    {
        if (featureDepthMm is null or <= 25m)
        {
            return 0m;
        }

        var depthBeyondInitialBand = featureDepthMm.Value - 25m;
        var increments = Math.Ceiling(depthBeyondInitialBand / 50m);

        return Math.Max(1m, increments);
    }

    private static decimal ResolveFeatureMinimumDraftDeg(DraftFeatureType featureType)
    {
        return featureType switch
        {
            DraftFeatureType.RibSidewall => 0.5m,
            DraftFeatureType.BossOuterWall => 0.5m,
            DraftFeatureType.BossInnerHoleCorePin => 0.25m,
            DraftFeatureType.HoleCorePin => 0.25m,
            DraftFeatureType.DeepWallOver25Mm => 1.0m,
            DraftFeatureType.Shutoff => 3.0m,
            _ => 0m
        };
    }

    private static decimal ResolveFeatureRecommendedDraftDeg(DraftFeatureType featureType)
    {
        return featureType switch
        {
            DraftFeatureType.RibSidewall => 0.5m,
            DraftFeatureType.BossOuterWall => 0.5m,
            DraftFeatureType.BossInnerHoleCorePin => 0.5m,
            DraftFeatureType.HoleCorePin => 0.5m,
            DraftFeatureType.DeepWallOver25Mm => 1.5m,
            DraftFeatureType.Shutoff => 3.0m,
            _ => 0m
        };
    }

    private static decimal ResolveFailBelowDeg(DraftRuleInput input)
    {
        return input.FeatureType switch
        {
            DraftFeatureType.Shutoff => 2.0m,
            DraftFeatureType.RibSidewall => 0.25m,
            DraftFeatureType.BossOuterWall => 0.25m,
            DraftFeatureType.BossInnerHoleCorePin => 0.1m,
            DraftFeatureType.HoleCorePin => 0.1m,
            DraftFeatureType.DeepWallOver25Mm => 0.5m,
            _ => ResolveMaterialSurfaceFailBelowDeg(input.Material, input.SurfaceType)
        };
    }

    private static decimal ResolveMaterialSurfaceFailBelowDeg(
        DraftMaterial material,
        DraftSurfaceType surfaceType)
    {
        if (surfaceType == DraftSurfaceType.TexturedLight)
        {
            return material == DraftMaterial.NylonPa ? 1.5m : 2.0m;
        }

        if (surfaceType == DraftSurfaceType.TexturedHeavy)
        {
            return material == DraftMaterial.NylonPa ? 3.0m : 3.5m;
        }

        return material switch
        {
            DraftMaterial.Pc => 1.0m,
            DraftMaterial.NylonPa => 0.25m,
            DraftMaterial.Pom => 0.25m,
            DraftMaterial.Pe => 0.25m,
            DraftMaterial.Pvc => 0.25m,
            _ => 0.5m
        };
    }

    private static bool IsStandardMoldReleaseFeature(DraftFeatureType featureType)
    {
        return featureType is
            DraftFeatureType.Wall or
            DraftFeatureType.RibSidewall or
            DraftFeatureType.BossOuterWall or
            DraftFeatureType.BossInnerHoleCorePin or
            DraftFeatureType.HoleCorePin or
            DraftFeatureType.DeepWallOver25Mm or
            DraftFeatureType.Shutoff;
    }

    private static string BuildRuleId(DraftRuleInput input)
    {
        return $"draft.{input.Material}.{input.SurfaceType}.{input.FeatureType}".ToLowerInvariant();
    }
}