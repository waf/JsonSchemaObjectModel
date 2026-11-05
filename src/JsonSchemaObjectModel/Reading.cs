using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel;

/// <summary>
/// Creates a keyword from its JSON value.
/// </summary>
/// <param name="value">The keyword value. Clone it if the keyword keeps it.</param>
/// <param name="context">Reads subschemas and reports errors with their location.</param>
public delegate IJsonSchemaKeyword JsonSchemaKeywordReader(JsonElement value, JsonSchemaReadContext context);

/// <summary>
/// A set of keyword readers. Parsing uses it to turn keyword names into typed keywords.
/// </summary>
/// <remarks>
/// - Parsing keeps a keyword with no reader as an <see cref="UnknownKeyword"/>. The schema still round-trips.
/// - A registry is mutable. <see cref="JsonSchemaReaderOptions"/> takes a snapshot of it.
/// </remarks>
public sealed class JsonSchemaKeywordRegistry
{
    private readonly Dictionary<string, JsonSchemaKeywordReader> readers = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a registry that contains the draft 2020-12 keywords.
    /// </summary>
    public static JsonSchemaKeywordRegistry CreateDraft202012()
    {
        var registry = new JsonSchemaKeywordRegistry();
        StandardKeywordReaders.AddDraft202012(registry);
        return registry;
    }

    /// <summary>
    /// The names of the keywords in the registry.
    /// </summary>
    public IEnumerable<string> KeywordNames => readers.Keys;

    /// <summary>
    /// Adds a keyword reader.
    /// </summary>
    /// <exception cref="ArgumentException">The registry already has a reader for <paramref name="name"/>.</exception>
    public JsonSchemaKeywordRegistry Add(string name, JsonSchemaKeywordReader reader)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(reader);
        if (!readers.TryAdd(name, reader))
        {
            throw new ArgumentException($"The registry already has a reader for '{name}'.", nameof(name));
        }

        return this;
    }

    /// <summary>
    /// Adds the reader of a keyword type that defines its own name and reader.
    /// </summary>
    /// <exception cref="ArgumentException">The registry already has a reader for the name of <typeparamref name="T"/>.</exception>
    public JsonSchemaKeywordRegistry Add<T>()
        where T : class, IJsonSchemaKeyword<T> => Add(T.KeywordName, static (value, context) => T.Read(value, context));

    /// <summary>
    /// Adds the reader of a keyword type, or replaces the reader that has the same name.
    /// </summary>
    public JsonSchemaKeywordRegistry Set<T>()
        where T : class, IJsonSchemaKeyword<T> => Set(T.KeywordName, static (value, context) => T.Read(value, context));

    /// <summary>
    /// Adds a keyword reader, or replaces the reader that has the same name.
    /// </summary>
    public JsonSchemaKeywordRegistry Set(string name, JsonSchemaKeywordReader reader)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(reader);
        readers[name] = reader;
        return this;
    }

    /// <summary>
    /// Removes a keyword reader. Parsing then keeps that keyword as an <see cref="UnknownKeyword"/>.
    /// </summary>
    public bool Remove(string name) => readers.Remove(name);

    //  Each entry keeps its name, so that a lookup by span can return the registry's string.
    internal FrozenDictionary<string, (string Name, JsonSchemaKeywordReader Reader)> Freeze() =>
        readers.ToFrozenDictionary(pair => pair.Key, pair => (pair.Key, pair.Value), StringComparer.Ordinal);
}

/// <summary>
/// Options for parsing a schema.
/// </summary>
/// <remarks>
/// An instance is immutable and safe to share between threads.
/// Create it once and reuse it.
/// </remarks>
public sealed class JsonSchemaReaderOptions
{
    private readonly FrozenDictionary<string, (string Name, JsonSchemaKeywordReader Reader)>.AlternateLookup<ReadOnlySpan<char>> readers;

    /// <summary>
    /// Creates options that read the keywords in <paramref name="keywords"/>.
    /// Later changes to the registry have no effect on these options.
    /// </summary>
    public JsonSchemaReaderOptions(JsonSchemaKeywordRegistry keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        readers = keywords.Freeze().GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// Options that read the draft 2020-12 keywords.
    /// </summary>
    public static JsonSchemaReaderOptions Default { get; } = new(JsonSchemaKeywordRegistry.CreateDraft202012());

    /// <summary>
    /// Finds the reader for a keyword name.
    /// The name of a registered keyword comes back as the registry's own string, so the caller need not allocate one.
    /// </summary>
    internal bool TryGetReader(ReadOnlySpan<char> name, out string registeredName, out JsonSchemaKeywordReader reader)
    {
        var found = readers.TryGetValue(name, out var entry);
        (registeredName, reader) = entry;
        return found;
    }
}

/// <summary>
/// The context in which a <see cref="JsonSchemaKeywordReader"/> runs.
/// </summary>
/// <remarks>
/// Use it to read subschemas and to create errors.
/// An error from <see cref="Error"/> gets the JSON Pointer of the keyword value as its <see cref="JsonException.Path"/>.
/// </remarks>
public readonly partial struct JsonSchemaReadContext
{
    internal JsonSchemaReadContext(JsonSchemaReaderOptions options, string keywordName)
    {
        Options = options;
        KeywordName = keywordName;
    }

    /// <summary>
    /// The options of the current parse.
    /// </summary>
    public JsonSchemaReaderOptions Options { get; }

    /// <summary>
    /// The name of the keyword that the reader is reading.
    /// </summary>
    public string KeywordName { get; }

    /// <summary>
    /// Reads a subschema that is the keyword value.
    /// </summary>
    public JsonSchema ReadSchema(JsonElement value) => JsonSchemaReader.Read(value, Options);

    /// <summary>
    /// Reads a subschema at an index in the keyword value.
    /// </summary>
    public JsonSchema ReadSchema(JsonElement value, int index)
    {
        try
        {
            return JsonSchemaReader.Read(value, Options);
        }
        catch (SchemaReadException exception)
        {
            exception.AddParent(index);
            throw;
        }
    }

    /// <summary>
    /// Reads a subschema at a property name in the keyword value.
    /// </summary>
    public JsonSchema ReadSchema(JsonElement value, string propertyName)
    {
        try
        {
            return JsonSchemaReader.Read(value, Options);
        }
        catch (SchemaReadException exception)
        {
            exception.AddParent(propertyName);
            throw;
        }
    }

    /// <summary>
    /// Creates an error for an invalid keyword value.
    /// </summary>
    public JsonException Error(string message) => new SchemaReadException(KeywordName, message);
}

/// <summary>
/// An error while parsing. It collects its location while it propagates, so a parse that succeeds allocates no paths.
/// </summary>
internal sealed class SchemaReadException : JsonException
{
    private readonly string? keywordName;
    private readonly string detail;

    //  The segments from the error up to the root.
    private readonly List<PathSegment> segmentsFromLeaf = [];

    public SchemaReadException(string? keywordName, string detail)
        : base(detail)
    {
        this.keywordName = keywordName;
        this.detail = detail;
    }

    public void AddParent(string name) => segmentsFromLeaf.Add(new(name, 0));

    public void AddParent(int index) => segmentsFromLeaf.Add(new(null, index));

    public JsonException ToPublicException()
    {
        var builder = new StringBuilder();
        for (var position = segmentsFromLeaf.Count - 1; position >= 0; position--)
        {
            builder.Append('/');
            var (name, index) = segmentsFromLeaf[position];
            builder.Append(
                name is null
                    ? index.ToString(CultureInfo.InvariantCulture)
                    : name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)
            );
        }

        var location = builder.ToString();
        var message = keywordName is null ? $"{detail} Location: '{location}'." : $"Invalid '{keywordName}' at '{location}': {detail}";
        return new JsonException(message, location, lineNumber: null, bytePositionInLine: null);
    }

    /// <summary>
    /// A property name, or an array index if the name is null.
    /// </summary>
    private readonly record struct PathSegment(string? Name, int Index);
}

internal static class JsonSchemaReader
{
    //  Keyword names longer than this are rare. They take the slower path that allocates the name.
    private const int MaxStackName = 64;

    /// <summary>
    /// Reads a root schema, and turns an internal error into a public one with the full location.
    /// </summary>
    public static JsonSchema ReadRoot(JsonElement element, JsonSchemaReaderOptions options)
    {
        try
        {
            return Read(element, options);
        }
        catch (SchemaReadException exception)
        {
            throw exception.ToPublicException();
        }
    }

    public static JsonSchema Read(JsonElement element, JsonSchemaReaderOptions options)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                return JsonSchema.True;
            case JsonValueKind.False:
                return JsonSchema.False;
            case JsonValueKind.Object:
                break;
            default:
                throw new SchemaReadException(null, $"A schema must be an object or a boolean, but the value is {element.ValueKind}.");
        }

        var count = element.GetPropertyCount();
        if (count == 0)
        {
            return JsonSchema.Empty;
        }

        var keywords = new IJsonSchemaKeyword[count];
        var position = 0;
        Span<char> nameBuffer = stackalloc char[MaxStackName];
        foreach (var property in element.EnumerateObject())
        {
            var reader = FindReader(property, options, nameBuffer, out var name);
            for (var earlier = 0; earlier < position; earlier++)
            {
                if (string.Equals(keywords[earlier].Name, name, StringComparison.Ordinal))
                {
                    var duplicate = new SchemaReadException(null, $"The schema has more than one '{name}' keyword.");
                    duplicate.AddParent(name);
                    throw duplicate;
                }
            }

            IJsonSchemaKeyword keyword;
            if (reader is null)
            {
                keyword = new UnknownKeyword(name, property.Value);
            }
            else
            {
                try
                {
                    keyword = reader(property.Value, new JsonSchemaReadContext(options, name));
                }
                catch (SchemaReadException exception)
                {
                    exception.AddParent(name);
                    throw;
                }

                if (!string.Equals(keyword.Name, name, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The reader for '{name}' returned a keyword named '{keyword.Name}'. The names must be the same."
                    );
                }
            }

            keywords[position++] = keyword;
        }

        return new JsonSchema(ImmutableCollectionsMarshal.AsImmutableArray(keywords));
    }

    /// <summary>
    /// Finds the reader for a property. The name of a known keyword is the registry's string, so no string is allocated.
    /// </summary>
    private static JsonSchemaKeywordReader? FindReader(
        JsonProperty property,
        JsonSchemaReaderOptions options,
        Span<char> nameBuffer,
        out string name
    )
    {
        var rawName = JsonMarshal.GetRawUtf8PropertyName(property);

        //  An escaped name, or a long one, takes the slow path.
        if (rawName.Length <= MaxStackName && !rawName.Contains((byte)'\\'))
        {
            var length = Encoding.UTF8.GetChars(rawName, nameBuffer);
            var chars = nameBuffer[..length];
            if (options.TryGetReader(chars, out var registeredName, out var reader))
            {
                name = registeredName;
                return reader;
            }

            name = new string(chars);
            return null;
        }

        name = property.Name;
        if (options.TryGetReader(name, out var registered, out var slowReader))
        {
            name = registered;
            return slowReader;
        }

        return null;
    }
}
