using System.Globalization;

namespace PicoMoldForge.Core.Engineering.Shrinkage;

/// <summary>
/// The shrinkage used to size the mold cavity. <paramref name="Source"/> says where it came from; <paramref name="Note"/>
/// states it, with the typical range when the resin is known.
/// </summary>
public sealed record MoldShrinkage(
    decimal Rate,
    string Source,
    string? ResinName,
    ShrinkageRuleRange? TypicalRange,
    string Note);

/// <summary>
/// Shrinkage belongs to the molded resin, not to the mold steel. A configured resin decides: its rate, or the
/// recommended rate of a known resin (ABS, PP, PC, PA/nylon, POM, PE) when no rate is given. Without a resin the
/// configured material.shrinkageRate is kept, as before.
/// </summary>
public static class MoldShrinkageResolver
{
    public static MoldShrinkage Resolve(string? resinName, decimal? resinShrinkageRate, decimal materialShrinkageRate)
    {
        if (materialShrinkageRate < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(materialShrinkageRate), "Shrinkage rate cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(resinName))
        {
            return new MoldShrinkage(
                materialShrinkageRate,
                "material.shrinkageRate",
                null,
                null,
                Invariant($"Shrinkage {Percent(materialShrinkageRate)} taken from material.shrinkageRate; add \"resin\": {{ \"name\": ..., \"shrinkageRate\": ... }} to give the shrinkage of the part resin."));
        }

        var name = resinName.Trim();
        ShrinkageRuleRange? range = TryParseResin(name, out var material)
            ? new ShrinkageRuleEngine().ResolveRange(material)
            : null;

        if (resinShrinkageRate is null)
        {
            if (range is null)
            {
                throw new ArgumentException(Invariant($"resin.shrinkageRate is required for resin '{name}' (known resins: ABS, PP, PC, PA, POM, PE)."), nameof(resinShrinkageRate));
            }

            return new MoldShrinkage(
                range.RecommendedRate,
                "resin (recommended rate)",
                name,
                range,
                Invariant($"Shrinkage {Percent(range.RecommendedRate)}: recommended rate for resin {name} (typical {Percent(range.MinimumRate)} to {Percent(range.MaximumRate)})."));
        }

        var rate = resinShrinkageRate.Value;

        if (rate < 0m || rate > 0.1m)
        {
            throw new ArgumentOutOfRangeException(nameof(resinShrinkageRate), "resin.shrinkageRate must be between 0 and 0.1.");
        }

        var note = range is null
            ? Invariant($"Shrinkage {Percent(rate)} from resin {name}.")
            : rate < range.MinimumRate || rate > range.MaximumRate
                ? Invariant($"Shrinkage {Percent(rate)} from resin {name} is outside its typical {Percent(range.MinimumRate)} to {Percent(range.MaximumRate)}: check the resin datasheet.")
                : Invariant($"Shrinkage {Percent(rate)} from resin {name} (typical {Percent(range.MinimumRate)} to {Percent(range.MaximumRate)}).");

        return new MoldShrinkage(rate, "resin", name, range, note);
    }

    public static bool TryParseResin(string name, out ShrinkageMaterial material)
    {
        var key = new string((name ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        material = key switch
        {
            "ABS" => ShrinkageMaterial.Abs,
            "PP" or "POLYPROPYLENE" => ShrinkageMaterial.Pp,
            "PC" or "POLYCARBONATE" => ShrinkageMaterial.Pc,
            "PA" or "PA6" or "PA66" or "NYLON" or "POLYAMIDE" => ShrinkageMaterial.NylonPa,
            "POM" or "ACETAL" => ShrinkageMaterial.Pom,
            "PE" or "HDPE" or "LDPE" or "POLYETHYLENE" => ShrinkageMaterial.Pe,
            _ => ShrinkageMaterial.General
        };

        return material != ShrinkageMaterial.General;
    }

    private static string Percent(decimal rate) => Invariant($"{rate * 100m:0.###} %");

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
