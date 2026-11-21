using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>The loose parameter bag of one effect: a value read is marked used, anything left over is an error.</summary>
internal sealed class V3Params(Dictionary<string, JsonElement>? raw, string path)
{
    private readonly Dictionary<string, JsonElement> _raw = raw ?? [];

    private readonly HashSet<string> _used = [];

    /// <summary>A number parameter, or the default when it is absent or null; infinity is refused.</summary>
    public double Number(string key, double fallback)
    {
        if (!Take(key, out JsonElement value))
        {
            return fallback;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number))
        {
            return number;
        }
        throw Wrong(key, "a finite number");
    }

    /// <summary>A true or false parameter, or the default when it is absent or null.</summary>
    public bool Flag(string key, bool fallback)
    {
        if (!Take(key, out JsonElement value))
        {
            return fallback;
        }
        bool isFlag = value.ValueKind is JsonValueKind.True or JsonValueKind.False;
        return isFlag ? value.GetBoolean() : throw Wrong(key, "true or false");
    }

    /// <summary>A text parameter, or the default when it is absent or null.</summary>
    public string Text(string key, string fallback)
    {
        if (!Take(key, out JsonElement value))
        {
            return fallback;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Wrong(key, "text");
    }

    /// <summary>Refuses a parameter that was not read, because version 4 has nowhere to keep it.</summary>
    public void RejectUnknown()
    {
        string? unknown = _raw.Keys.FirstOrDefault(key => !_used.Contains(key));
        if (unknown is not null)
        {
            throw V3Context.Fail($"{path}.params.{unknown}", "is not a parameter of this effect; v4 cannot keep it.");
        }
    }

    private bool Take(string key, out JsonElement value)
    {
        _used.Add(key);
        return _raw.TryGetValue(key, out value) && value.ValueKind != JsonValueKind.Null;
    }

    private ProjectFormatException Wrong(string key, string expected)
    {
        return V3Context.Fail($"{path}.params.{key}", $"must be {expected}.");
    }
}
