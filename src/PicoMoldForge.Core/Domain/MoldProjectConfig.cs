using System;
using System.Collections.Generic;
using System.IO;

namespace PicoMoldForge.Core.Domain;

public enum MoldMode
{
    Unknown = 0,
    Prototype = 1,
    ProductionPreliminary = 2
}

public enum MoldStandard
{
    Custom = 0,
    HASCO = 1,
    DME = 2
}

public sealed record MaterialProfile
{
    public string Name { get; init; } = "Generic Tool Steel";

    public decimal ShrinkageRate { get; init; } = 0.012m;
}

public sealed record MachineProfile
{
    public string Name { get; init; } = "Generic Metal AM Machine";

    public decimal BuildVolumeXmm { get; init; } = 250m;

    public decimal BuildVolumeYmm { get; init; } = 250m;

    public decimal BuildVolumeZmm { get; init; } = 250m;
}

public sealed record MoldProjectConfig
{
    public string ProjectName { get; init; } = "PicoMoldForge Project";

    public string InputPath { get; init; } = string.Empty;

    public string OutputDirectory { get; init; } = "output";

    public MoldMode Mode { get; init; } = MoldMode.Prototype;

    public MoldStandard Standard { get; init; } = MoldStandard.Custom;

    public decimal VoxelResolutionMm { get; init; } = 1.0m;

    public MaterialProfile Material { get; init; } = new();

    public MachineProfile Machine { get; init; } = new();

    public string? StepConverterPath { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            errors.Add("ProjectName is required.");
        }

        if (string.IsNullOrWhiteSpace(InputPath))
        {
            errors.Add("InputPath is required.");
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            errors.Add("OutputDirectory is required.");
        }

        if (Mode == MoldMode.Unknown)
        {
            errors.Add("Mode must be specified.");
        }

        if (VoxelResolutionMm <= 0)
        {
            errors.Add("VoxelResolutionMm must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(Material.Name))
        {
            errors.Add("Material.Name is required.");
        }

        if (Material.ShrinkageRate < 0)
        {
            errors.Add("Material.ShrinkageRate cannot be negative.");
        }

        if (Machine.BuildVolumeXmm <= 0 || Machine.BuildVolumeYmm <= 0 || Machine.BuildVolumeZmm <= 0)
        {
            errors.Add("Machine build volume dimensions must be greater than zero.");
        }

        ValidateInputExtension(errors);

        return errors;
    }

    private void ValidateInputExtension(List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            return;
        }

        var extension = Path.GetExtension(InputPath);

        if (extension.Equals(".stl", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (extension.Equals(".step", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".stp", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(StepConverterPath))
            {
                errors.Add("STEP input requires StepConverterPath. Native STEP import is not enabled in this phase.");
            }

            return;
        }

        errors.Add("InputPath must point to an STL file. STEP requires a configured external converter.");
    }
}