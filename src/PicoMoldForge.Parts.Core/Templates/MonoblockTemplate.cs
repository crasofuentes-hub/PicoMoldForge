using System.Globalization;
using PicoMoldForge.Parts.Axes;
using PicoMoldForge.Parts.Features;
using PicoMoldForge.Parts.Geometry;
using PicoMoldForge.Parts.Profiles;
using PicoMoldForge.Parts.Verification;

namespace PicoMoldForge.Parts.Templates;

/// <summary>
/// Simplified inline engine block (millimeters). X = crankshaft axis, Z = cylinder axis, deck at Z = Height.
/// Open-deck water jacket, crankcase open at the bottom, one bulkhead between each pair of cylinders.
/// </summary>
public sealed record MonoblockParameters
{
    public int Cylinders { get; init; } = 2;

    public double Bore { get; init; } = 40.0;

    public double CylinderSpacing { get; init; } = 52.0;

    public double LinerThickness { get; init; } = 4.0;

    public double JacketWidth { get; init; } = 5.0;

    public double JacketDepth { get; init; } = 45.0;

    public double Length { get; init; } = 120.0;

    public double Width { get; init; } = 84.0;

    public double Height { get; init; } = 110.0;

    public double CrankcaseHeight { get; init; } = 50.0;

    public double WallThickness { get; init; } = 6.0;

    public double BulkheadThickness { get; init; } = 10.0;

    public double MainBearingDiameter { get; init; } = 30.0;

    public double CrankAxisHeight { get; init; } = 30.0;

    public double BoltHoleDiameter { get; init; } = 6.8;

    public double BoltDepth { get; init; } = 30.0;

    public double BoltOffsetY { get; init; } = 35.5;
}

/// <summary>
/// Monoblock as linear sweeps: block dropped along Z, open-deck jacket and bores drilled along Z,
/// crankcase bays dropped along Z from below, main bearing line bored along X, head bolt holes along Z.
/// </summary>
public static class MonoblockTemplate
{
    public const string Type = "monoblock";

    public static PartDefinition Build(string id, string? title, MonoblockParameters p, MeshSettings mesh)
    {
        ArgumentNullException.ThrowIfNull(p);

        var errors = new List<string>();
        var n = p.Cylinders;
        var boreRadius = p.Bore / 2;
        var linerRadius = boreRadius + p.LinerThickness;
        var jacketRadius = linerRadius + p.JacketWidth;
        var mainRadius = p.MainBearingDiameter / 2;
        var boltRadius = p.BoltHoleDiameter / 2;
        var halfLength = p.Length / 2;
        var halfWidth = p.Width / 2;
        var centers = Enumerable.Range(0, Math.Max(n, 1)).Select(i => (i - ((n - 1) / 2.0)) * p.CylinderSpacing).ToArray();
        var boltXs = Enumerable.Range(0, Math.Max(n, 1) + 1).Select(k => centers[0] - (p.CylinderSpacing / 2) + (k * p.CylinderSpacing)).ToArray();

        if (n < 1 || n > 8)
        {
            errors.Add("cylinders must be between 1 and 8.");
        }

        if (!(p.Bore > 5 && p.LinerThickness > 1 && p.JacketWidth > 1))
        {
            errors.Add("bore must be > 5 mm, linerThickness > 1 mm and jacketWidth > 1 mm.");
        }

        if (n > 1 && !(p.CylinderSpacing > 2 * linerRadius))
        {
            errors.Add("cylinderSpacing must be larger than the liner diameter (liners cannot overlap).");
        }

        if (!(halfLength - (centers[^1] + jacketRadius) >= 3))
        {
            errors.Add("length leaves less than 3 mm of wall outside the end water jackets.");
        }

        if (!(halfWidth - jacketRadius >= 3))
        {
            errors.Add("width leaves less than 3 mm of wall outside the water jacket.");
        }

        if (!(p.CrankcaseHeight > 5 && p.JacketDepth > 1 && p.Height - p.JacketDepth > p.CrankcaseHeight + 3))
        {
            errors.Add("The water jacket must end at least 3 mm above the crankcase.");
        }

        if (!(p.WallThickness >= 2 && halfWidth - p.WallThickness > boreRadius + 0.5))
        {
            errors.Add("Side walls must be at least 2 mm and leave room for the bores to open into the crankcase.");
        }

        var bayEdges = new List<(double Start, double End)>();

        for (var i = 0; i < n; i++)
        {
            var start = i == 0 ? -halfLength + p.WallThickness : ((centers[i - 1] + centers[i]) / 2) + (p.BulkheadThickness / 2);
            var end = i == n - 1 ? halfLength - p.WallThickness : ((centers[i] + centers[i + 1]) / 2) - (p.BulkheadThickness / 2);
            bayEdges.Add((start, end));

            if (!(centers[i] - boreRadius > start + 0.5 && centers[i] + boreRadius < end - 0.5))
            {
                errors.Add(FormattableString.Invariant($"Cylinder {i + 1} does not fit inside its crankcase bay (reduce bore or bulkheadThickness)."));
            }
        }

        if (!(p.CrankAxisHeight - mainRadius > 2 && p.CrankAxisHeight + mainRadius < p.CrankcaseHeight - 2))
        {
            errors.Add("The main bearing bore must stay inside the crankcase height with 2 mm margins.");
        }

        if (!(p.BoltDepth > 1 && p.Height - p.BoltDepth > p.CrankcaseHeight + 2))
        {
            errors.Add("Head bolt holes must end at least 2 mm above the crankcase.");
        }

        foreach (var bx in boltXs)
        {
            var nearest = centers.Min(c => Math.Sqrt(((bx - c) * (bx - c)) + (p.BoltOffsetY * p.BoltOffsetY)));

            if (!(nearest - jacketRadius - boltRadius >= 2))
            {
                errors.Add(FormattableString.Invariant($"Head bolt at X={bx:0.###} is closer than 2 mm to the water jacket."));
            }

            if (!(halfLength - Math.Abs(bx) - boltRadius >= 2 && halfWidth - p.BoltOffsetY - boltRadius >= 2))
            {
                errors.Add(FormattableString.Invariant($"Head bolt at X={bx:0.###} is closer than 2 mm to an outer face."));
            }
        }

        if (errors.Count > 0)
        {
            throw new PartDefinitionException(errors.Distinct().ToArray());
        }

        Vec2 Center2(double x) => new(x, 0);

        var jacketProfile = new ProfileDifference(
            new ProfileUnion(centers.Select(c => (IProfile2D)new CircleProfile(Center2(c), jacketRadius))),
            new ProfileUnion(centers.Select(c => (IProfile2D)new CircleProfile(Center2(c), linerRadius))));

        var cylinderInstances = centers.Select(c => RigidTransform.FromTranslation(new Vec3(c, 0, 0))).ToArray();
        var bayInstances = bayEdges.Select(b => RigidTransform.FromTranslation(new Vec3((b.Start + b.End) / 2, 0, 0))).ToArray();
        var boltInstances = boltXs
            .SelectMany(x => new[] { new Vec3(x, p.BoltOffsetY, 0), new Vec3(x, -p.BoltOffsetY, 0) })
            .Select(RigidTransform.FromTranslation)
            .ToArray();
        var bayHalfLength = (bayEdges[0].End - bayEdges[0].Start) / 2;

        var features = new List<FeatureDefinition>
        {
            new(
                "block",
                "Bloque: rectángulo que cae en Z",
                FeatureOperation.Add,
                new LinearSweepDefinition(LinearAxis.Z, p.Height, 0),
                new RectangleProfile(new Vec2(-halfLength, -halfWidth), new Vec2(halfLength, halfWidth)),
                Placement.Single(Vec3.Zero)),
            new(
                "water-jacket",
                "Camisa de agua (cubierta abierta): anillo que baja en Z, restando",
                FeatureOperation.Subtract,
                new LinearSweepDefinition(LinearAxis.Z, p.Height + 1, p.Height - p.JacketDepth),
                jacketProfile,
                Placement.Single(Vec3.Zero)),
            new(
                "bores",
                "Cilindros: taladro en Z, restando",
                FeatureOperation.Subtract,
                new LinearSweepDefinition(LinearAxis.Z, p.Height + 1, p.CrankcaseHeight - 1),
                new CircleProfile(Vec2.Zero, boreRadius),
                cylinderInstances),
            new(
                "crankcase",
                "Cárter: bahías que suben en Z desde abajo, restando",
                FeatureOperation.Subtract,
                new LinearSweepDefinition(LinearAxis.Z, -1, p.CrankcaseHeight),
                new RectangleProfile(new Vec2(-bayHalfLength, -(halfWidth - p.WallThickness)), new Vec2(bayHalfLength, halfWidth - p.WallThickness)),
                bayInstances),
            new(
                "main-bearings",
                "Línea de bancada: barreno en X, restando",
                FeatureOperation.Subtract,
                new LinearSweepDefinition(LinearAxis.X, -halfLength - 1, halfLength + 1),
                new CircleProfile(new Vec2(0, p.CrankAxisHeight), mainRadius),
                Placement.Single(Vec3.Zero)),
            new(
                "head-bolts",
                "Barrenos de culata: taladro ciego en Z, restando",
                FeatureOperation.Subtract,
                new LinearSweepDefinition(LinearAxis.Z, p.Height + 1, p.Height - p.BoltDepth),
                new CircleProfile(Vec2.Zero, boltRadius),
                boltInstances)
        };

        if (n > 1 && bayEdges.Select(b => b.End - b.Start).Distinct().Count() > 1)
        {
            // End bays can be longer than inner bays; use one feature per bay so each keeps its own length.
            features[3] = new FeatureDefinition(
                "crankcase",
                "Cárter: bahías que suben en Z desde abajo, restando",
                FeatureOperation.Subtract,
                new LinearSweepDefinition(LinearAxis.Z, -1, p.CrankcaseHeight),
                new ProfileUnion(bayEdges.Select(b => (IProfile2D)new RectangleProfile(
                    new Vec2(b.Start, -(halfWidth - p.WallThickness)),
                    new Vec2(b.End, halfWidth - p.WallThickness)))),
                Placement.Single(Vec3.Zero));
        }

        var first = centers[0];
        var checks = new List<IPartCheck>
        {
            new SurfacePointCheck("block.bore", "Diámetro de cilindro = " + Mm(p.Bore), new Vec3(first + boreRadius, 0, p.Height - 10)),
            new SurfacePointCheck("block.liner", "Camisa seca de " + Mm(p.LinerThickness) + " alrededor del cilindro", new Vec3(first - linerRadius, 0, p.Height - 10)),
            new SurfacePointCheck("block.jacket-outer", "Camisa de agua de " + Mm(p.JacketWidth) + " de ancho", new Vec3(first, jacketRadius, p.Height - 10)),
            new SurfacePointCheck("block.jacket-floor", "Fondo de la camisa de agua a " + Mm(p.Height - p.JacketDepth), new Vec3(first, linerRadius + (p.JacketWidth / 2), p.Height - p.JacketDepth)),
            new SurfacePointCheck("block.deck", "Cubierta a Z = " + Mm(p.Height), new Vec3(first, halfWidth - 1.0, p.Height)),
            new SurfacePointCheck("block.main-bearing", "Bancada Ø" + Mm(p.MainBearingDiameter) + " a Z = " + Mm(p.CrankAxisHeight), new Vec3(-halfLength + (p.WallThickness / 2), 0, p.CrankAxisHeight + mainRadius)),
            new SurfacePointCheck("block.crankcase-top", "Techo del cárter a Z = " + Mm(p.CrankcaseHeight), new Vec3((bayEdges[0].Start + first - boreRadius) / 2, 0, p.CrankcaseHeight)),
            new ContainmentCheck("block.bolt-hole", "Barreno de culata abierto en la cubierta", new Vec3(boltXs[0], p.BoltOffsetY, p.Height - (p.BoltDepth / 2)), inside: false),
            new ContainmentCheck("block.bolt-blind", "Barreno de culata ciego (material bajo el fondo)", new Vec3(boltXs[0], p.BoltOffsetY, p.Height - p.BoltDepth - 1), inside: true)
        };

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("Cilindros", n.ToString(CultureInfo.InvariantCulture)),
            new("Diámetro de cilindro", Mm(p.Bore)),
            new("Distancia entre cilindros", Mm(p.CylinderSpacing)),
            new("Bloque (L x A x H)", FormattableString.Invariant($"{p.Length:0.##} x {p.Width:0.##} x {p.Height:0.##} mm")),
            new("Camisa seca / de agua", FormattableString.Invariant($"{p.LinerThickness:0.##} / {p.JacketWidth:0.##} mm, profundidad {p.JacketDepth:0.##} mm")),
            new("Bancada", FormattableString.Invariant($"Ø{p.MainBearingDiameter:0.##} mm a Z = {p.CrankAxisHeight:0.##} mm")),
            new("Barrenos de culata", FormattableString.Invariant($"{boltInstances.Length} x Ø{p.BoltHoleDiameter:0.##} mm, {p.BoltDepth:0.##} mm de profundidad"))
        };

        return new PartDefinition(
            id,
            title ?? FormattableString.Invariant($"Monoblock {n} cilindros Ø{p.Bore:0.##} mm (simplificado)"),
            Type,
            parameters,
            features,
            new PartExpectations(
                new Box3(new Vec3(-halfLength, -halfWidth, 0), new Vec3(halfLength, halfWidth, p.Height)),
                null,
                Genus: (2 * n) + 1,
                checks),
            mesh);
    }

    private static string Mm(double value) => value.ToString("0.####", CultureInfo.InvariantCulture) + " mm";
}
