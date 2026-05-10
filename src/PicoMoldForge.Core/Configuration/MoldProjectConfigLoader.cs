using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PicoMoldForge.Core.Domain;

namespace PicoMoldForge.Core.Configuration;

public sealed class ConfigLoadException : Exception
{
    public ConfigLoadException(string message)
        : base(message)
    {
    }

    public ConfigLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class MoldProjectConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public static MoldProjectConfig LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConfigLoadException("Config path is required.");
        }

        if (!File.Exists(path))
        {
            throw new ConfigLoadException($"Config file was not found: {path}");
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<MoldProjectConfig>(json, JsonOptions);

            if (config is null)
            {
                throw new ConfigLoadException("Config file did not contain a valid project configuration.");
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw new ConfigLoadException("Config file contains invalid JSON.", ex);
        }
    }
}