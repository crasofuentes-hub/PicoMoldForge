namespace PicoMoldForge.Generator;

public static class BinaryStlProbe
{
    public static bool IsBinaryStl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("STL path is required.", nameof(path));
        }

        var resolvedPath = Path.GetFullPath(path);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("STL file was not found.", resolvedPath);
        }

        var length = new FileInfo(resolvedPath).Length;

        if (length < 84)
        {
            return false;
        }

        using var stream = File.OpenRead(resolvedPath);
        using var reader = new BinaryReader(stream);

        var header = reader.ReadBytes(80);
        var triangleCount = reader.ReadUInt32();
        var expectedLength = 84L + (triangleCount * 50L);

        if (expectedLength == length)
        {
            return true;
        }

        var headerText = System.Text.Encoding.ASCII.GetString(header).TrimStart();

        if (headerText.StartsWith("solid", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return false;
    }
}