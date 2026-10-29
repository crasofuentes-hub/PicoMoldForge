using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PicoMoldForge.Parts.Cli;

/// <summary>
/// Serves an output folder (viewer page, meshes, reports) on 127.0.0.1 only. Browsers block fetch() from
/// file:// pages, so the viewer needs a local HTTP origin. Responses are never cached, so a regenerated part
/// is always the one shown.
/// </summary>
public sealed class ViewerServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private ViewerServer(WebApplication app, string address)
    {
        _app = app;
        Address = address;
    }

    public string Address { get; }

    public static async Task<ViewerServer> StartAsync(string root, int port, CancellationToken cancellationToken = default)
    {
        var fullRoot = Path.GetFullPath(root);

        if (!File.Exists(Path.Combine(fullRoot, PartsRunner.ViewerFileName)))
        {
            throw new FileNotFoundException("The output folder has no viewer page. Run 'demo' or 'generate' first.", Path.Combine(fullRoot, PartsRunner.ViewerFileName));
        }

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = fullRoot,
            Args = Array.Empty<string>()
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        var app = builder.Build();
        var files = new PhysicalFileProvider(fullRoot);
        var types = new FileExtensionContentTypeProvider();
        types.Mappings[".bin"] = "application/octet-stream";
        types.Mappings[".stl"] = "model/stl";
        types.Mappings[".json"] = "application/json; charset=utf-8";
        types.Mappings[".txt"] = "text/plain; charset=utf-8";

        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ContentTypeProvider = types });

        await app.StartAsync(cancellationToken);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? $"http://127.0.0.1:{port}";
        return new ViewerServer(app, address.TrimEnd('/') + "/");
    }

    public Task WaitForShutdownAsync(CancellationToken cancellationToken) => _app.WaitForShutdownAsync(cancellationToken);

    public static bool TryOpenBrowser(string url, out string? error)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            error = ex.Message;
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
