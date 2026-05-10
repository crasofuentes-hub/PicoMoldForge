namespace PicoMoldForge.Core.Input;

public interface IPartInputConverter
{
    PartInputFormat SourceFormat { get; }

    PartInputFormat TargetFormat { get; }

    PartInputLoadResult ConvertToStl(string inputPath, string outputDirectory);
}