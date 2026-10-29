using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using PicoMoldForge.Parts.Cli;
using PicoMoldForge.Parts.PicoGK;

namespace PicoMoldForge.Parts.Cli.Tests;

public sealed class PartsCommandLineTests
{
    [Fact]
    public void Help_DescribesTheCommandsAndTheAxes()
    {
        var (code, output, _) = Run("--help");

        Assert.Equal(0, code);
        Assert.Contains("Uso: PicoMoldForge.Parts.Cli", output, StringComparison.Ordinal);
        Assert.Contains("ISO 841", output, StringComparison.Ordinal);
        Assert.Contains("CAM", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_IsPrinted()
    {
        var (code, output, _) = Run("--version");

        Assert.Equal(0, code);
        Assert.StartsWith("PicoMoldForge.Parts 1.0.0", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fly")]
    [InlineData("demo", "--bogus")]
    [InlineData("demo", "--only", "unicorn")]
    [InlineData("generate")]
    [InlineData("generate", "--sample", "spur-gear", "--quick", "--resolution", "1")]
    [InlineData("generate", "--sample", "spur-gear", "--resolution", "0.001")]
    [InlineData("view", "--port", "70000")]
    public void UsageErrors_ExitWithTwo(params string[] args)
    {
        var (code, _, error) = Run(args);

        Assert.Equal(2, code);
        Assert.StartsWith("ERROR", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Samples_ListTheEmbeddedRecipes()
    {
        var (code, output, _) = Run("samples");

        Assert.Equal(0, code);
        Assert.Equal(new[] { "spur-gear", "helical-gear", "piston", "monoblock", "flange" }, output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    [Fact]
    public void SelfTest_Passes()
    {
        var (code, output, _) = Run("selftest");

        Assert.Equal(0, code);
        Assert.Contains("SELFTEST: PASS", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WritesAVerifiedPartAndAManifestWithMatchingHashes()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "out");

        var (code, text, error) = Run("generate", "--sample", "spur-gear", "--resolution", "0.5", "--output", output);

        Assert.True(code == 0, text + error);
        Assert.Contains("PARTS: PASS", text, StringComparison.Ordinal);

        foreach (var file in new[] { "index.html", "parts.json", "summary.txt", "RunManifest.json", "spur-gear/spur-gear.stl", "spur-gear/recipe.json", "spur-gear/part-report.json" })
        {
            Assert.True(File.Exists(Path.Combine(output, file)), file + " missing");
        }

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "RunManifest.json")));
        Assert.Equal("PASS", manifest.RootElement.GetProperty("status").GetString());

        foreach (var artifact in manifest.RootElement.GetProperty("artifacts").EnumerateArray())
        {
            var path = Path.Combine(output, artifact.GetProperty("path").GetString()!);
            using var stream = File.OpenRead(path);
            Assert.Equal(artifact.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
        }

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "parts.json")));
        var part = index.RootElement.GetProperty("parts")[0];
        Assert.Equal("spur-gear", part.GetProperty("id").GetString());
        Assert.Equal("spur-gear/spur-gear.stl", part.GetProperty("files").GetProperty("stl").GetString());
    }

    [Fact]
    public void Generate_RefusesAForeignNonEmptyFolder()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "index.html"), "<p>my own page</p>");

        var (code, _, error) = Run("generate", "--sample", "spur-gear", "--resolution", "0.6", "--output", temp.Path);

        Assert.Equal(2, code);
        Assert.Contains("not a PicoMoldForge Parts output", error, StringComparison.Ordinal);
        Assert.Equal("<p>my own page</p>", File.ReadAllText(Path.Combine(temp.Path, "index.html")));
    }

    [Fact]
    public void InvalidRecipe_ExitsWithTwo()
    {
        using var temp = new TempDirectory();
        var recipe = Path.Combine(temp.Path, "bad.json");
        File.WriteAllText(recipe, "{ \"schemaVersion\": \"picomoldforge.part-recipe.v1\", \"name\": \"bad\", \"template\": { \"type\": \"spurGear\", \"teeth\": 2 } }");

        var (code, _, error) = Run("generate", "--recipe", recipe, "--output", Path.Combine(temp.Path, "out"));

        Assert.Equal(2, code);
        Assert.Contains("bad.json", error, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanOutput_RemovesOnlyWhatPartsWrote()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "out");
        Assert.Equal(0, Run("generate", "--sample", "spur-gear", "--resolution", "0.6", "--output", output).Code);
        var notes = Path.Combine(output, "my-notes.txt");
        File.WriteAllText(notes, "keep me");
        var userFileInPart = Path.Combine(output, "spur-gear", "photo.jpg");
        File.WriteAllText(userFileInPart, "keep me too");

        var (code, text, error) = Run("generate", "--sample", "flange", "--resolution", "0.8", "--output", output, "--clean-output");

        Assert.True(code == 0, text + error);
        Assert.True(File.Exists(notes));
        Assert.True(File.Exists(userFileInPart));
        Assert.False(File.Exists(Path.Combine(output, "spur-gear", "spur-gear.stl")));
        Assert.True(File.Exists(Path.Combine(output, "flange", "flange.stl")));

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "parts.json")));
        Assert.Equal(new[] { "flange" }, index.RootElement.GetProperty("parts").EnumerateArray().Select(p => p.GetProperty("id").GetString()));
    }

    [Fact]
    public void PicoGkBackend_ReportsWhenTheRuntimeIsMissing()
    {
        if (PicoGkRuntimeAvailability.IsAvailable)
        {
            // Covered by the opt-in PicoMoldForge.Parts.PicoGK.Tests on machines with the native runtime.
            return;
        }

        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "out");

        var (code, text, _) = Run("generate", "--sample", "spur-gear", "--resolution", "0.6", "--output", output, "--backend", "picogk");

        Assert.Equal(1, code);
        Assert.Contains("PicoGK: FAIL - PicoGK no disponible", text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, "spur-gear", "picogk-crosscheck.json")));
    }

    internal static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = PartsCommandLine.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}

public sealed class ViewerServerTests
{
    [Fact]
    public async Task Server_ServesTheOutputFolderWithoutCaching()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "index.html"), "<!doctype html><title>t</title>");
        File.WriteAllText(Path.Combine(temp.Path, "parts.json"), "{\"parts\":[]}");
        Directory.CreateDirectory(Path.Combine(temp.Path, "gear"));
        File.WriteAllBytes(Path.Combine(temp.Path, "gear", "preview.bin"), new byte[] { 1, 2, 3 });

        await using var server = await ViewerServer.StartAsync(temp.Path, 0);
        using var client = new HttpClient { BaseAddress = new Uri(server.Address) };

        Assert.StartsWith("http://127.0.0.1:", server.Address, StringComparison.Ordinal);

        var page = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("no-store", page.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("<title>t</title>", await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var json = await client.GetAsync("/parts.json");
        Assert.Equal("application/json", json.Content.Headers.ContentType?.MediaType);

        var binary = await client.GetAsync("/gear/preview.bin");
        Assert.Equal(new byte[] { 1, 2, 3 }, await binary.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/missing.json")).StatusCode);
    }

    [Fact]
    public async Task Server_RefusesAFolderWithoutAViewer()
    {
        using var temp = new TempDirectory();

        await Assert.ThrowsAsync<FileNotFoundException>(() => ViewerServer.StartAsync(temp.Path, 0));
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pmf-parts-cli-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
