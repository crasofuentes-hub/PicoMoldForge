using System.Text.Json;
using PicoMoldForge.Core.BooleanGeometry;
using PicoMoldForge.Core.Configuration;
using PicoMoldForge.Core.Input;
using PicoMoldForge.Core.Parting;

namespace PicoMoldForge.Generator;

public sealed class GeneratorPipelineInputValidator
{
    public GeneratorPipelineInput Validate(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("Config path is required.", nameof(configPath));
        }

        var resolvedConfigPath = Path.GetFullPath(configPath);

        if (!File.Exists(resolvedConfigPath))
        {
            throw new FileNotFoundException("Config file was not found.", resolvedConfigPath);
        }

        var config = MoldProjectConfigLoader.LoadFromFile(resolvedConfigPath);
        var validationErrors = config.Validate();

        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Config validation failed: " + string.Join(" ", validationErrors));
        }

        using var document = JsonDocument.Parse(File.ReadAllText(resolvedConfigPath));

        var moldBlockBounds = LoadMoldBlockBounds(document);
        var moldBlockErrors = moldBlockBounds.Validate();

        if (moldBlockErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "moldBlock validation failed: " + string.Join(" ", moldBlockErrors));
        }

        var partingOverride = LoadPartingOverride(document, moldBlockBounds);

        var cooling = LoadCoolingConfig(document);
        var coolingErrors = cooling.Validate();

        if (coolingErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "cooling validation failed: " + string.Join(" ", coolingErrors));
        }

        var lattice = LoadLatticeConfig(document);
        var latticeErrors = lattice.Validate();

        if (latticeErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "lattice validation failed: " + string.Join(" ", latticeErrors));
        }

        var moldSystem = LoadMoldSystemConfig(document);
        var moldSystemErrors = moldSystem.Validate();

        if (moldSystemErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "moldSystem validation failed: " + string.Join(" ", moldSystemErrors));
        }

        var dfam = LoadDfamConfig(document);
        var dfamErrors = dfam.Validate();

        if (dfamErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "dfam validation failed: " + string.Join(" ", dfamErrors));
        }

        var configDirectory = Path.GetDirectoryName(resolvedConfigPath) ?? Directory.GetCurrentDirectory();

        var resolvedInputPath = Path.IsPathRooted(config.InputPath)
            ? Path.GetFullPath(config.InputPath)
            : Path.GetFullPath(Path.Combine(configDirectory, config.InputPath));

        var resolvedOutputDirectory = Path.IsPathRooted(config.OutputDirectory)
            ? Path.GetFullPath(config.OutputDirectory)
            : Path.GetFullPath(Path.Combine(configDirectory, config.OutputDirectory));

        var loader = new PartInputLoader();
        var inputResult = loader.Load(config with
        {
            InputPath = resolvedInputPath,
            OutputDirectory = resolvedOutputDirectory
        });

        if (!inputResult.IsSuccessful)
        {
            throw new InvalidOperationException(
                "Input validation failed: " + inputResult.Error);
        }

        if (!BinaryStlProbe.IsBinaryStl(resolvedInputPath))
        {
            throw new InvalidOperationException(
                "PicoMoldForge Generator requires binary STL input for the PicoGK pipeline. ASCII STL is not supported by the current generator path.");
        }

        return new GeneratorPipelineInput(
            config,
            resolvedConfigPath,
            resolvedInputPath,
            resolvedOutputDirectory,
            moldBlockBounds,
            partingOverride,
            cooling,
            lattice,
            moldSystem,
            dfam);
    }

    private static GeneratorDfamConfig LoadDfamConfig(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("dfam", out var dfam))
        {
            throw new InvalidOperationException("dfam is required for DfAM report generation.");
        }

        return new GeneratorDfamConfig(
            MinimumWallThicknessMm: ReadRequiredDecimal(dfam, "minimumWallThicknessMm"),
            RecommendedMinimumWallThicknessMm: ReadRequiredDecimal(dfam, "recommendedMinimumWallThicknessMm"),
            UsesPreliminaryGeometry: ReadRequiredBoolean(dfam, "usesPreliminaryGeometry"));
    }

    private static GeneratorMoldSystemConfig LoadMoldSystemConfig(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("moldSystem", out var moldSystem))
        {
            throw new InvalidOperationException("moldSystem is required for MoldSystemDiagnostic.stl generation.");
        }

        return new GeneratorMoldSystemConfig(
            PartSizeXmm: ReadRequiredDecimal(moldSystem, "partSizeXmm"),
            PartSizeYmm: ReadRequiredDecimal(moldSystem, "partSizeYmm"),
            PartSizeZmm: ReadRequiredDecimal(moldSystem, "partSizeZmm"),
            MoldMarginMm: ReadRequiredDecimal(moldSystem, "moldMarginMm"),
            EjectorPinDiameterMm: ReadRequiredDecimal(moldSystem, "ejectorPinDiameterMm"),
            EjectorPinCount: ReadRequiredInt32(moldSystem, "ejectorPinCount"),
            VentWidthMm: ReadRequiredDecimal(moldSystem, "ventWidthMm"),
            VentDepthMm: ReadRequiredDecimal(moldSystem, "ventDepthMm"),
            InsertClearanceMm: ReadRequiredDecimal(moldSystem, "insertClearanceMm"));
    }

    private static GeneratorLatticeConfig LoadLatticeConfig(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("lattice", out var lattice))
        {
            throw new InvalidOperationException("lattice is required for LatticeDiagnostic.stl generation.");
        }

        return new GeneratorLatticeConfig(
            RegionName: ReadRequiredString(lattice, "regionName"),
            MinXmm: ReadRequiredDecimal(lattice, "minXmm"),
            MinYmm: ReadRequiredDecimal(lattice, "minYmm"),
            MinZmm: ReadRequiredDecimal(lattice, "minZmm"),
            MaxXmm: ReadRequiredDecimal(lattice, "maxXmm"),
            MaxYmm: ReadRequiredDecimal(lattice, "maxYmm"),
            MaxZmm: ReadRequiredDecimal(lattice, "maxZmm"),
            CellSizeMm: ReadRequiredDecimal(lattice, "cellSizeMm"),
            BeamRadiusMm: ReadRequiredDecimal(lattice, "beamRadiusMm"),
            TargetRelativeDensity: ReadRequiredDecimal(lattice, "targetRelativeDensity"));
    }

    private static GeneratorCoolingConfig LoadCoolingConfig(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("cooling", out var cooling))
        {
            throw new InvalidOperationException("cooling is required for CoolingDiagnostic.stl generation.");
        }

        return new GeneratorCoolingConfig(
            PartSizeXmm: ReadRequiredDecimal(cooling, "partSizeXmm"),
            PartSizeYmm: ReadRequiredDecimal(cooling, "partSizeYmm"),
            PartSizeZmm: ReadRequiredDecimal(cooling, "partSizeZmm"),
            ChannelDiameterMm: ReadRequiredDecimal(cooling, "channelDiameterMm"),
            ChannelSpacingMm: ReadRequiredDecimal(cooling, "channelSpacingMm"),
            MinimumClearanceMm: ReadRequiredDecimal(cooling, "minimumClearanceMm"),
            ChannelCount: ReadRequiredInt32(cooling, "channelCount"));
    }

    private static MoldBlockBounds LoadMoldBlockBounds(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("moldBlock", out var moldBlock))
        {
            throw new InvalidOperationException("moldBlock is required for boolean cavity generation.");
        }

        return new MoldBlockBounds(
            MinXmm: ReadRequiredDecimal(moldBlock, "minXmm"),
            MinYmm: ReadRequiredDecimal(moldBlock, "minYmm"),
            MinZmm: ReadRequiredDecimal(moldBlock, "minZmm"),
            MaxXmm: ReadRequiredDecimal(moldBlock, "maxXmm"),
            MaxYmm: ReadRequiredDecimal(moldBlock, "maxYmm"),
            MaxZmm: ReadRequiredDecimal(moldBlock, "maxZmm"));
    }

    private static GeneratorPartingOverride? LoadPartingOverride(JsonDocument document, MoldBlockBounds moldBlockBounds)
    {
        if (!document.RootElement.TryGetProperty("parting", out var parting))
        {
            return null;
        }

        var mode = ReadRequiredString(parting, "mode");

        if (string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.Equals(mode, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("parting.mode must be either Auto or Manual.");
        }

        var axisText = ReadRequiredString(parting, "axis");

        if (!Enum.TryParse<PartingAxis>(axisText, ignoreCase: true, out var axis))
        {
            throw new InvalidOperationException("parting.axis must be one of: X, Y, Z.");
        }

        var offsetMm = ReadRequiredDecimal(parting, "offsetMm");

        ValidatePartingOffsetInsideMoldBlock(axis, offsetMm, moldBlockBounds);

        return new GeneratorPartingOverride(axis, offsetMm);
    }

    private static void ValidatePartingOffsetInsideMoldBlock(
        PartingAxis axis,
        decimal offsetMm,
        MoldBlockBounds moldBlockBounds)
    {
        var minimum = axis switch
        {
            PartingAxis.X => moldBlockBounds.MinXmm,
            PartingAxis.Y => moldBlockBounds.MinYmm,
            PartingAxis.Z => moldBlockBounds.MinZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };

        var maximum = axis switch
        {
            PartingAxis.X => moldBlockBounds.MaxXmm,
            PartingAxis.Y => moldBlockBounds.MaxYmm,
            PartingAxis.Z => moldBlockBounds.MaxZmm,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unsupported parting axis.")
        };

        if (offsetMm <= minimum || offsetMm >= maximum)
        {
            throw new InvalidOperationException("parting.offsetMm must be inside moldBlock bounds for the selected axis.");
        }
    }

    private static decimal ReadRequiredDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            throw new InvalidOperationException($"{propertyName} is required.");
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"{propertyName} must be a number.");
        }

        return value.GetDecimal();
    }

    private static int ReadRequiredInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            throw new InvalidOperationException($"{propertyName} is required.");
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"{propertyName} must be a number.");
        }

        if (!value.TryGetInt32(out var result))
        {
            throw new InvalidOperationException($"{propertyName} must be a valid integer.");
        }

        return result;
    }

    private static bool ReadRequiredBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            throw new InvalidOperationException($"{propertyName} is required.");
        }

        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException($"{propertyName} must be a boolean.");
        }

        return value.GetBoolean();
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            throw new InvalidOperationException($"{propertyName} is required.");
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"{propertyName} must be a string.");
        }

        var text = value.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"{propertyName} cannot be empty.");
        }

        return text;
    }
}