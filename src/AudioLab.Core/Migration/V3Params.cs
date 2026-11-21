using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>The loose parameter bag of one effect. Reading a value marks it used; anything left over is an error.</summary>
internal sealed class V3Params(Dictionary<string, JsonElement>? raw, string path)
{
    private readonly Dictionary<string, JsonElement> _raw = raw ?? [];

    private readonly HashSet<string> _used = [];

    public double Number(string key, double fallback)
    {
        if (!Take(key, out JsonElement value))
        {
            return fallback;
        }
        return value.ValueKind == JsonValueKind.Number ? value.GetDouble() : throw Wrong(key, "a number");
    }

    public bool Flag(string key, bool fallback)
    {
        if (!Take(key, out JsonElement value))
        {
            return fallback;
        }
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw Wrong(key, "true or false");
    }

    public string Text(string key, string fallback)
    {
        if (!Take(key, out JsonElement value))
        {
            return fallback;
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Wrong(key, "text");
    }

    public void RejectUnknown()
    {
        string? unknown = _raw.Keys.FirstOrDefault(key => !_used.Contains(key));
        if (unknown is not null)
        {
            throw V3Context.Fail($"{path}.params.{unknown}", "is not a parameter of this effect, and version 4 cannot keep it.");
        }
    }

    private bool Take(string key, out JsonElement value)
    {
        _used.Add(key);
        return _raw.TryGetValue(key, out value) && value.ValueKind != JsonValueKind.Null;
    }

    private ProjectFormatException Wrong(string key, string expected) => V3Context.Fail($"{path}.params.{key}", $"must be {expected}.");
}
