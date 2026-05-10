using System.Text.Json;
using System.Text.Json.Serialization;

namespace PicoMoldForge.Core.Exports;

public sealed class FinalReportJsonWriter
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public string Serialize(FinalProjectReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var validationErrors = report.Validate();

        if (validationErrors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid final project report: " + string.Join(" ", validationErrors),
                nameof(report));
        }

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public void WriteToFile(FinalProjectReport report, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        var resolvedPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(resolvedPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var json = Serialize(report);

        File.WriteAllText(resolvedPath, json);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}