using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AudioLab.Core.Commands;

/// <summary>Collects the commands of every domain, then builds the one immutable registry.</summary>
public sealed class CommandRegistryBuilder
{
    /// <summary>The longest wire name of a command, in characters.</summary>
    public const int MaxNameLength = 64;

    /// <summary>The longest description of a command, in characters.</summary>
    public const int MaxDescriptionLength = 500;

    private readonly Dictionary<string, CommandRegistration> _byName = new(StringComparer.Ordinal);

    private readonly Dictionary<Type, CommandRegistration> _byType = [];

    /// <summary>Whether <paramref name="name"/> is a wire name: lower-case letters, digits and underscores, starting with a letter.</summary>
    public static bool IsValidName(string? name)
    {
        return name is { Length: >= 1 and <= MaxNameLength }
            && name[0] is >= 'a' and <= 'z'
            && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
    }

    /// <summary>Registers one command type, the handler that applies it and the strict JSON that carries it.</summary>
    /// <param name="name">The stable wire name, such as <c>track_rename</c>; an MCP tool takes the same name.</param>
    /// <param name="description">One sentence: what the command does and which ids it allocates.</param>
    /// <param name="json">The type info from a source-generated context that refuses unknown, missing and null members.</param>
    /// <param name="handler">Applies the command to a document.</param>
    /// <exception cref="ArgumentException">The name or description is malformed, the type is not sealed, or the JSON rules are lax.</exception>
    /// <exception cref="InvalidOperationException">The name or the command type is already registered.</exception>
    public CommandRegistryBuilder Add<TCommand>(
        string name,
        string description,
        JsonTypeInfo<TCommand> json,
        CommandHandler<TCommand> handler)
        where TCommand : Command
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(handler);
        if (!IsValidName(name))
        {
            throw new ArgumentException("A command name is lower-case letters, digits and underscores, up to 64.", nameof(name));
        }
        if (string.IsNullOrWhiteSpace(description) || description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"A description is 1 to {MaxDescriptionLength} characters.", nameof(description));
        }
        if (!typeof(TCommand).IsSealed)
        {
            throw new ArgumentException("A command type is sealed, so the registry finds its handler by exact type.", nameof(json));
        }
        RequireStrict(json.Options);
        if (_byName.ContainsKey(name) || _byType.ContainsKey(typeof(TCommand)))
        {
            throw new InvalidOperationException($"The command {name} or the type {typeof(TCommand).Name} is already registered.");
        }
        CommandDescriptor descriptor = new(name, description, typeof(TCommand), CommandSchema.For(json));
        TypedCommandRegistration<TCommand> registration = new(descriptor, json, handler);
        _byName.Add(name, registration);
        _byType.Add(typeof(TCommand), registration);
        return this;
    }

    /// <summary>Builds the registry; the builder can keep adding, and each registry is independent.</summary>
    public CommandRegistry Build() => new(_byName.Values);

    // A lax context would accept a misspelt member as an edit that does nothing, so the wire rules are not optional.
    private static void RequireStrict(JsonSerializerOptions options)
    {
        bool strict = options.UnmappedMemberHandling == JsonUnmappedMemberHandling.Disallow
            && options.RespectNullableAnnotations
            && options.RespectRequiredConstructorParameters
            && !options.AllowDuplicateProperties
            && !options.PropertyNameCaseInsensitive
            && !options.AllowTrailingCommas
            && options.ReadCommentHandling == JsonCommentHandling.Disallow
            && options.NumberHandling == JsonNumberHandling.Strict
            && options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase;
        if (!strict)
        {
            throw new ArgumentException(
                "The command context must be as strict as ProjectJsonContext: refuse unknown, null and duplicate members, "
                + "numbers in text, other letter case, trailing commas and comments, and name members in camel case.",
                nameof(options));
        }
    }
}
