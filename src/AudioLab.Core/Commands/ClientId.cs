using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Names the client that sends a command, such as a hub connection or an agent session, so gestures never cross clients.</summary>
/// <remarks>Opaque to Core: the caller supplies it and Core only compares it. It never enters a document or the wire format.</remarks>
public readonly record struct ClientId
{
    /// <summary>The longest client id, in characters.</summary>
    public const int MaxLength = 128;

    private readonly string? _value;

    private ClientId(string value) => _value = value;

    /// <summary>The identifier text; empty only for <c>default</c>, which no call accepts.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Wraps the caller's own id for a client: 1 to <see cref="MaxLength"/> characters, no control characters.</summary>
    /// <exception cref="ArgumentException">The text is empty, too long or holds control or text-direction characters.</exception>
    public static ClientId From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength || !TextRules.IsClean(value))
        {
            throw new ArgumentException($"A client id is 1 to {MaxLength} characters without control characters.", nameof(value));
        }
        return new ClientId(value);
    }

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
