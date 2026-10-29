using System.Text.Json;
using PicoMoldForge.Parts.Geometry;

namespace PicoMoldForge.Parts.Recipes;

/// <summary>
/// Strict JSON object reader: records every property it was asked for and reports the rest as unknown,
/// with JSON paths in every error message.
/// </summary>
internal sealed class JsonObjectReader
{
    private readonly JsonElement _element;
    private readonly List<string> _errors;
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    public JsonObjectReader(JsonElement element, string path, List<string> errors)
    {
        _element = element;
        Path = path;
        _errors = errors;
    }

    public string Path { get; }

    public bool IsObject => _element.ValueKind == JsonValueKind.Object;

    public bool Has(string name) => IsObject && _element.TryGetProperty(name, out _);

    public string? RequiredString(string name) => ReadString(name, required: true);

    public string? OptionalString(string name) => ReadString(name, required: false);

    public double? RequiredDouble(string name) => ReadDouble(name, required: true);

    public double? OptionalDouble(string name) => ReadDouble(name, required: false);

    public int? RequiredInt(string name) => ReadInt(name, required: true);

    public int? OptionalInt(string name) => ReadInt(name, required: false);

    public bool? OptionalBool(string name)
    {
        if (!TryGet(name, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        _errors.Add($"{Path}.{name}: must be true or false.");
        return null;
    }

    public JsonObjectReader? Object(string name) => ReadObject(name, required: true);

    public JsonObjectReader? OptionalObject(string name) => ReadObject(name, required: false);

    public IReadOnlyList<JsonObjectReader>? Array(string name) => ReadObjectArray(name, required: true);

    public IReadOnlyList<JsonObjectReader>? OptionalArray(string name) => ReadObjectArray(name, required: false);

    public Vec2? RequiredVec2(string name) => ReadVec2(name, required: true);

    public Vec2? OptionalVec2(string name) => ReadVec2(name, required: false);

    public Vec3? RequiredVec3(string name) => ReadVec3(name, required: true);

    public Vec3? OptionalVec3(string name) => ReadVec3(name, required: false);

    public IReadOnlyList<Vec2>? RequiredVec2Array(string name)
    {
        if (!TryGet(name, out var value))
        {
            _errors.Add($"{Path}.{name}: is required.");
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            _errors.Add($"{Path}.{name}: must be an array of [u, v] points.");
            return null;
        }

        var result = new List<Vec2>();
        var index = 0;

        foreach (var item in value.EnumerateArray())
        {
            var numbers = Numbers(item, 2, $"{Path}.{name}[{index}]");

            if (numbers is null)
            {
                return null;
            }

            result.Add(new Vec2(numbers[0], numbers[1]));
            index++;
        }

        return result;
    }

    public IReadOnlyList<Vec3>? OptionalVec3Array(string name)
    {
        if (!TryGet(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            _errors.Add($"{Path}.{name}: must be a non-empty array of [x, y, z] points.");
            return null;
        }

        var result = new List<Vec3>();
        var index = 0;

        foreach (var item in value.EnumerateArray())
        {
            var numbers = Numbers(item, 3, $"{Path}.{name}[{index}]");

            if (numbers is null)
            {
                return null;
            }

            result.Add(new Vec3(numbers[0], numbers[1], numbers[2]));
            index++;
        }

        return result;
    }

    public IReadOnlyList<double>? OptionalDoubleArray(string name)
    {
        if (!TryGet(name, out var value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            _errors.Add($"{Path}.{name}: must be an array of numbers.");
            return null;
        }

        var result = new List<double>();
        var index = 0;

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var number) || !double.IsFinite(number))
            {
                _errors.Add($"{Path}.{name}[{index}]: must be a finite number.");
                return null;
            }

            result.Add(number);
            index++;
        }

        return result;
    }

    public void RejectUnknown()
    {
        if (!IsObject)
        {
            return;
        }

        foreach (var property in _element.EnumerateObject())
        {
            if (!_used.Contains(property.Name))
            {
                _errors.Add($"{Path}.{property.Name}: unknown property.");
            }
        }
    }

    private bool TryGet(string name, out JsonElement value)
    {
        _used.Add(name);

        if (IsObject && _element.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null)
        {
            return true;
        }

        value = default;
        return false;
    }

    private string? ReadString(string name, bool required)
    {
        if (!TryGet(name, out var value))
        {
            if (required)
            {
                _errors.Add($"{Path}.{name}: is required.");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            _errors.Add($"{Path}.{name}: must be a string.");
            return null;
        }

        return value.GetString();
    }

    private double? ReadDouble(string name, bool required)
    {
        if (!TryGet(name, out var value))
        {
            if (required)
            {
                _errors.Add($"{Path}.{name}: is required.");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
        {
            _errors.Add($"{Path}.{name}: must be a finite number.");
            return null;
        }

        return number;
    }

    private int? ReadInt(string name, bool required)
    {
        if (!TryGet(name, out var value))
        {
            if (required)
            {
                _errors.Add($"{Path}.{name}: is required.");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            _errors.Add($"{Path}.{name}: must be an integer.");
            return null;
        }

        return number;
    }

    private JsonObjectReader? ReadObject(string name, bool required)
    {
        if (!TryGet(name, out var value))
        {
            if (required)
            {
                _errors.Add($"{Path}.{name}: is required.");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            _errors.Add($"{Path}.{name}: must be an object.");
            return null;
        }

        return new JsonObjectReader(value, $"{Path}.{name}", _errors);
    }

    private IReadOnlyList<JsonObjectReader>? ReadObjectArray(string name, bool required)
    {
        if (!TryGet(name, out var value))
        {
            if (required)
            {
                _errors.Add($"{Path}.{name}: is required.");
            }

            return null;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            _errors.Add($"{Path}.{name}: must be an array.");
            return null;
        }

        var result = new List<JsonObjectReader>();
        var index = 0;

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                _errors.Add($"{Path}.{name}[{index}]: must be an object.");
            }
            else
            {
                result.Add(new JsonObjectReader(item, $"{Path}.{name}[{index}]", _errors));
            }

            index++;
        }

        return result;
    }

    private Vec2? ReadVec2(string name, bool required)
    {
        if (!TryGet(name, out var value))
        {
            if (required)
            {
                _errors.Add($"{Path}.{name}: is required.");
            }

            return null;
        }

        var numbers = Numbers(value, 2, $"{Path}.{name}");
        return numbers is null ? null : new Vec2(numbers[0], numbers[1]);
    }

    private Vec3? ReadVec3(string name, bool required)
    {
        if (!TryGet(name, out var value))
        {
            if (required)
            {
                _errors.Add($"{Path}.{name}: is required.");
            }

            return null;
        }

        var numbers = Numbers(value, 3, $"{Path}.{name}");
        return numbers is null ? null : new Vec3(numbers[0], numbers[1], numbers[2]);
    }

    private double[]? Numbers(JsonElement value, int count, string path)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != count)
        {
            _errors.Add($"{path}: must be an array of {count} numbers.");
            return null;
        }

        var result = new double[count];
        var i = 0;

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var number) || !double.IsFinite(number))
            {
                _errors.Add($"{path}[{i}]: must be a finite number.");
                return null;
            }

            result[i++] = number;
        }

        return result;
    }
}
