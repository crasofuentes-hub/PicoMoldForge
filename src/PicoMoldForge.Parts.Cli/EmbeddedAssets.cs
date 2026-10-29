using System.Reflection;
using System.Text;

namespace PicoMoldForge.Parts.Cli;

/// <summary>Sample recipes and the viewer page are embedded in the executable so the demo works from any folder.</summary>
public static class EmbeddedAssets
{
    private const string SamplePrefix = "PicoMoldForge.Parts.Samples.";
    private const string ViewerResource = "PicoMoldForge.Parts.Viewer.index.html";

    /// <summary>Order in which the demo builds the sample parts.</summary>
    public static readonly string[] DemoOrder = { "spur-gear", "helical-gear", "piston", "monoblock", "flange" };

    public static IReadOnlyList<(string Name, string Json)> SampleRecipes()
    {
        var assembly = typeof(EmbeddedAssets).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(SamplePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n[SamplePrefix.Length..^".json".Length])
            .ToList();

        return names
            .OrderBy(n => Array.IndexOf(DemoOrder, n) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(n => n, StringComparer.Ordinal)
            .Select(n => (n, Read(assembly, SamplePrefix + n + ".json")))
            .ToArray();
    }

    public static string ViewerHtml() => Read(typeof(EmbeddedAssets).Assembly, ViewerResource);

    private static string Read(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource '{resource}' is missing from the build.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
