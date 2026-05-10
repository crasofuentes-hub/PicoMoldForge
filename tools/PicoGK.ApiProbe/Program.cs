using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: PicoGK.ApiProbe <path-to-PicoGK.dll>");
    return 2;
}

var assemblyPath = Path.GetFullPath(args[0]);
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

Console.WriteLine("# PicoGK Boolean Operations Probe");
Console.WriteLine();
Console.WriteLine($"Assembly: {assembly.FullName}");
Console.WriteLine();

var targetTypeNames = new[]
{
    "PicoGK.Voxels",
    "PicoGK.Mesh",
    "PicoGK.Lattice",
    "PicoGK.BBox3",
    "PicoGK.Library"
};

foreach (var typeName in targetTypeNames)
{
    DumpType(typeName);
}

Console.WriteLine("# Candidate primitive / implicit / boolean-related types");
Console.WriteLine();

var candidateTypes = assembly
    .GetTypes()
    .Where(type =>
        type.FullName is not null &&
        (
            type.FullName.Contains("Implicit", StringComparison.OrdinalIgnoreCase) ||
            type.FullName.Contains("Bound", StringComparison.OrdinalIgnoreCase) ||
            type.FullName.Contains("Box", StringComparison.OrdinalIgnoreCase) ||
            type.FullName.Contains("Cube", StringComparison.OrdinalIgnoreCase) ||
            type.FullName.Contains("Plane", StringComparison.OrdinalIgnoreCase) ||
            type.FullName.Contains("Sphere", StringComparison.OrdinalIgnoreCase) ||
            type.FullName.Contains("Cylinder", StringComparison.OrdinalIgnoreCase) ||
            type.FullName.Contains("Bool", StringComparison.OrdinalIgnoreCase) ||
            type.FullName.Contains("Voxel", StringComparison.OrdinalIgnoreCase)
        ))
    .OrderBy(type => type.FullName)
    .ToArray();

foreach (var type in candidateTypes)
{
    Console.WriteLine($"## {type.FullName}");
    Console.WriteLine();

    DumpConstructors(type);
    DumpRelevantMethods(type);
    Console.WriteLine();
}

return 0;

void DumpType(string typeName)
{
    var type = assembly.GetType(typeName);

    Console.WriteLine($"# {typeName}");
    Console.WriteLine();

    if (type is null)
    {
        Console.WriteLine("TYPE NOT FOUND");
        Console.WriteLine();
        return;
    }

    DumpConstructors(type);
    DumpRelevantMethods(type);
    Console.WriteLine();
}

void DumpConstructors(Type type)
{
    Console.WriteLine("## Constructors");

    var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

    if (constructors.Length == 0)
    {
        Console.WriteLine("- None");
        Console.WriteLine();
        return;
    }

    foreach (var constructor in constructors)
    {
        var parameters = string.Join(", ", constructor.GetParameters().Select(FormatParameter));
        Console.WriteLine($"- {type.Name}({parameters})");
    }

    Console.WriteLine();
}

void DumpRelevantMethods(Type type)
{
    Console.WriteLine("## Relevant methods");

    var keywords = new[]
    {
        "Bool",
        "Add",
        "Subtract",
        "Sub",
        "Intersect",
        "Intersection",
        "Difference",
        "Cut",
        "Clip",
        "Trim",
        "Offset",
        "Shell",
        "Hollow",
        "Render",
        "Mesh",
        "Voxel",
        "AsMesh",
        "Save",
        "Calculate",
        "Bounding",
        "BBox",
        "From",
        "Create"
    };

    var methods = type
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(method => keywords.Any(keyword => method.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        .OrderBy(method => method.Name)
        .ThenBy(method => method.GetParameters().Length)
        .ToArray();

    if (methods.Length == 0)
    {
        Console.WriteLine("- None");
        Console.WriteLine();
        return;
    }

    foreach (var method in methods)
    {
        var staticPrefix = method.IsStatic ? "static " : "";
        var parameters = string.Join(", ", method.GetParameters().Select(FormatParameter));
        Console.WriteLine($"- {staticPrefix}{FormatType(method.ReturnType)} {method.Name}({parameters})");
    }

    Console.WriteLine();
}

string FormatParameter(ParameterInfo parameter)
{
    var modifier =
        parameter.IsOut ? "out " :
        parameter.ParameterType.IsByRef ? "ref/in " :
        string.Empty;

    var parameterType = parameter.ParameterType.IsByRef
        ? parameter.ParameterType.GetElementType()
        : parameter.ParameterType;

    return $"{modifier}{FormatType(parameterType!)} {parameter.Name}";
}

string FormatType(Type type)
{
    if (type.FullName is null)
    {
        return type.Name;
    }

    if (!type.IsGenericType)
    {
        return type.FullName;
    }

    var genericName = type.FullName.Split('`')[0];
    var args = string.Join(", ", type.GetGenericArguments().Select(FormatType));

    return $"{genericName}<{args}>";
}