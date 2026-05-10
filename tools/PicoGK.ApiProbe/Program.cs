using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: PicoGK.ApiProbe <path-to-PicoGK.dll>");
    return 2;
}

var assemblyPath = Path.GetFullPath(args[0]);
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

var typeNames = new[]
{
    "PicoGK.Voxels",
    "PicoGK.Lattice",
    "PicoGK.Mesh",
    "PicoGK.BBox3"
};

foreach (var typeName in typeNames)
{
    var type = assembly.GetType(typeName);

    Console.WriteLine($"# {typeName}");
    Console.WriteLine();

    if (type is null)
    {
        Console.WriteLine("TYPE NOT FOUND");
        Console.WriteLine();
        continue;
    }

    Console.WriteLine("## Constructors");
    foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
    {
        var parameters = string.Join(", ", constructor.GetParameters().Select(parameter =>
            $"{parameter.ParameterType.FullName} {parameter.Name}"));

        Console.WriteLine($"- {type.Name}({parameters})");
    }

    Console.WriteLine();
    Console.WriteLine("## Cooling-relevant methods");

    var methodNames = new[]
    {
        "voxLatticeBeam",
        "RenderLattice",
        "RenderMesh",
        "mshAsMesh",
        "SaveToStlFile",
        "CalculateProperties",
        "oCalculateBoundingBox",
        "nAddBeam",
        "AddBeam",
        "Add",
        "AddVertex",
        "nAddVertex",
        "nAddTriangle",
        "nVertexCount",
        "nTriangleCount"
    };

    var methods = type
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(method => methodNames.Any(name =>
            method.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(method => method.Name)
        .ToArray();

    if (methods.Length == 0)
    {
        Console.WriteLine("- None");
    }

    foreach (var method in methods)
    {
        var parameters = string.Join(", ", method.GetParameters().Select(parameter =>
            $"{parameter.ParameterType.FullName} {parameter.Name}"));

        var staticPrefix = method.IsStatic ? "static " : "";
        Console.WriteLine($"- {staticPrefix}{method.ReturnType.FullName} {method.Name}({parameters})");
    }

    Console.WriteLine();
}

return 0;