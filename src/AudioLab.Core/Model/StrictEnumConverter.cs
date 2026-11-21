using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Writes an enum as its member name and refuses numbers and unknown names.</summary>
/// <typeparam name="TEnum">The enum type; its members carry a member name attribute.</typeparam>
public sealed class StrictEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Creates the converter with integer values switched off.</summary>
    public StrictEnumConverter()
        : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
