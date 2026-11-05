using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonSchemaObjectModel;

/// <summary>
/// An immutable JSON Schema: either a boolean schema or a schema object with an ordered list of keywords.
/// </summary>
/// <remarks>
/// - Parse a schema with <see cref="Parse(string, JsonSchemaReaderOptions?)"/> or <see cref="FromJsonElement"/>.
/// - Build a schema with <see cref="JsonSchemaBuilder"/>.
/// - Write a schema with <see cref="WriteTo"/> or <see cref="ToJsonString"/>.
/// - The type has a System.Text.Json converter that reads with <see cref="JsonSchemaReaderOptions.Default"/>.
///   To read custom keywords, add a <see cref="JsonSchemaConverter"/> with your own options.
/// </remarks>
[JsonConverter(typeof(JsonSchemaConverter))]
public sealed class JsonSchema
{
    private const int MaxStackBytes = 1024;

    [ThreadStatic]
    private static PooledJsonWriter? cachedWriter;

    private readonly ImmutableArray<IJsonSchemaKeyword> keywords;

    private JsonSchema(bool value)
    {
        BooleanValue = value;
        keywords = [];
    }

    internal JsonSchema(ImmutableArray<IJsonSchemaKeyword> keywords) => this.keywords = keywords;

    /// <summary>
    /// The boolean schema <c>true</c>. It accepts all instances.
    /// </summary>
    public static JsonSchema True { get; } = new(true);

    /// <summary>
    /// The boolean schema <c>false</c>. It rejects all instances.
    /// </summary>
    public static JsonSchema False { get; } = new(false);

    /// <summary>
    /// The empty schema object <c>{}</c>.
    /// </summary>
    public static JsonSchema Empty { get; } = new(ImmutableArray<IJsonSchemaKeyword>.Empty);

    /// <summary>
    /// The value of a boolean schema, or <see langword="null"/> for a schema object.
    /// </summary>
    public bool? BooleanValue { get; }

    /// <summary>
    /// The keywords of a schema object, in document order.
    /// A boolean schema has no keywords.
    /// </summary>
    public ImmutableArray<IJsonSchemaKeyword> Keywords => keywords;

    /// <summary>
    /// Converts a boolean to a boolean schema.
    /// </summary>
    public static implicit operator JsonSchema(bool value) => value ? True : False;

    /// <summary>
    /// Parses a schema from JSON text.
    /// </summary>
    /// <exception cref="JsonException">The text is not valid JSON, or it is not a valid schema.</exception>
    public static JsonSchema Parse(string json, JsonSchemaReaderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        var maxLength = Encoding.UTF8.GetMaxByteCount(json.Length);
        var rented = maxLength > MaxStackBytes ? ArrayPool<byte>.Shared.Rent(maxLength) : null;
        try
        {
            var buffer = rented ?? stackalloc byte[MaxStackBytes];
            var length = Encoding.UTF8.GetBytes(json, buffer);
            return Parse(buffer[..length], options);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Parses a schema from UTF-8 JSON text.
    /// </summary>
    /// <exception cref="JsonException">The text is not valid JSON, or it is not a valid schema.</exception>
    public static JsonSchema Parse(ReadOnlySpan<byte> utf8Json, JsonSchemaReaderOptions? options = null)
    {
        var reader = new Utf8JsonReader(utf8Json);

        //  ParseValue gives an element that owns its memory. Keywords that keep JSON values then
        //  reuse that memory, instead of copying each value into a document of its own.
        var element = JsonElement.ParseValue(ref reader);
        if (reader.Read())
        {
            throw new JsonException("The text contains more than one JSON value.");
        }

        return JsonSchemaReader.ReadRoot(element, options ?? JsonSchemaReaderOptions.Default);
    }

    /// <summary>
    /// Reads a schema from a JSON element.
    /// The schema does not keep a reference to the element's document if that document is disposable.
    /// </summary>
    /// <exception cref="JsonException">The element is not a valid schema.</exception>
    public static JsonSchema FromJsonElement(JsonElement element, JsonSchemaReaderOptions? options = null) =>
        JsonSchemaReader.ReadRoot(element, options ?? JsonSchemaReaderOptions.Default);

    /// <summary>
    /// Returns the first keyword of type <typeparamref name="T"/>, or <see langword="null"/>.
    /// </summary>
    public T? GetKeyword<T>()
        where T : class, IJsonSchemaKeyword
    {
        foreach (var keyword in keywords)
        {
            if (keyword is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the first keyword of type <typeparamref name="T"/>.
    /// </summary>
    public bool TryGetKeyword<T>([NotNullWhen(true)] out T? keyword)
        where T : class, IJsonSchemaKeyword
    {
        keyword = GetKeyword<T>();
        return keyword is not null;
    }

    /// <summary>
    /// Returns the keyword with the given name, or <see langword="null"/>.
    /// </summary>
    public IJsonSchemaKeyword? GetKeyword(string name) => GetKeyword(name.AsSpan());

    /// <summary>
    /// Returns the keyword with the given name, or <see langword="null"/>.
    /// </summary>
    public IJsonSchemaKeyword? GetKeyword(ReadOnlySpan<char> name)
    {
        foreach (var keyword in keywords)
        {
            if (name.SequenceEqual(keyword.Name))
            {
                return keyword;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a subschema with an RFC 6901 JSON Pointer, for example <c>/properties/menus/prefixItems/0</c>.
    /// </summary>
    /// <remarks>
    /// - The pointer can also be a URI fragment that starts with <c>#</c>.
    /// - The search follows keywords that implement <see cref="ISingleSubschemaKeyword"/>,
    ///   <see cref="ISubschemaArrayKeyword"/> or <see cref="ISubschemaMapKeyword"/>.
    /// - The search does not resolve <c>$ref</c>.
    /// </remarks>
    /// <returns>The subschema, or <see langword="null"/> if the pointer does not identify a subschema.</returns>
    /// <exception cref="FormatException">The pointer is not a valid JSON Pointer.</exception>
    public JsonSchema? FindSubschema(string pointer)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        return FindSubschema(pointer.AsSpan());
    }

    /// <inheritdoc cref="FindSubschema(string)"/>
    public JsonSchema? FindSubschema(ReadOnlySpan<char> pointer)
    {
        if (pointer.StartsWith('#'))
        {
            pointer = pointer[1..];
            if (pointer.Contains('%'))
            {
                return FindSubschema(Uri.UnescapeDataString(pointer.ToString()).AsSpan());
            }
        }

        var tokens = new JsonPointerTokenizer(pointer);
        var current = this;
        while (tokens.TryReadToken(out var token))
        {
            switch (current.FindKeywordForToken(token))
            {
                case ISingleSubschemaKeyword single:
                    current = single.Schema;
                    break;
                case ISubschemaArrayKeyword array:
                    if (!tokens.TryReadToken(out var indexToken) || !TryParseArrayIndex(indexToken, out var index))
                    {
                        return null;
                    }

                    if (index >= array.Schemas.Length)
                    {
                        return null;
                    }

                    current = array.Schemas[index];
                    break;
                case ISubschemaMapKeyword map:
                    if (!tokens.TryReadToken(out var keyToken) || !TryGetNamedSchema(map, keyToken, out var named))
                    {
                        return null;
                    }

                    current = named;
                    break;
                default:
                    return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Writes the schema as JSON.
    /// </summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (BooleanValue is { } value)
        {
            writer.WriteBooleanValue(value);
            return;
        }

        writer.WriteStartObject();
        foreach (var keyword in keywords)
        {
            writer.WritePropertyName(keyword.Name);
            keyword.WriteValue(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Returns the schema as JSON text.
    /// </summary>
    /// <remarks>
    /// - If <paramref name="options"/> has no encoder, the method uses <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>.
    ///   It escapes only what JSON requires, so a <c>pattern</c> such as <c>^[a-z]+$</c> and non-ASCII text stay readable.
    /// - "Unsafe" applies only if you put the text into HTML or a script block. The JSON is always valid.
    ///   For HTML-safe output, set <see cref="JsonWriterOptions.Encoder"/> to <see cref="JavaScriptEncoder.Default"/>.
    /// - The encoder still escapes characters outside the Basic Multilingual Plane, for example emoji.
    /// - <see cref="WriteTo"/> and <see cref="JsonSchemaConverter"/> use the encoder of the caller's writer.
    /// - With the default options, the method reuses a buffer and a writer for each thread, so it allocates only the result.
    /// </remarks>
    public string ToJsonString(JsonWriterOptions options = default)
    {
        options.Encoder ??= JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        if (!PooledJsonWriter.CanReuse(options))
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                WriteTo(writer);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        //  Take the cached writer, so that a nested call on the same thread gets a new one.
        var pooled = cachedWriter ?? new PooledJsonWriter();
        cachedWriter = null;
        try
        {
            var writer = pooled.Start();
            WriteTo(writer);
            writer.Flush();
            return Encoding.UTF8.GetString(pooled.WrittenSpan);
        }
        finally
        {
            if (pooled.TryFinish())
            {
                cachedWriter = pooled;
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => ToJsonString();

    private static bool TryGetNamedSchema(ISubschemaMapKeyword map, ReadOnlySpan<char> token, [NotNullWhen(true)] out JsonSchema? schema) =>
        token.Contains('~')
            ? map.Schemas.TryGetValue(JsonPointerTokenizer.Unescape(token), out schema)
            : map.Schemas.TryGetValue(token, out schema);

    private IJsonSchemaKeyword? FindKeywordForToken(ReadOnlySpan<char> token) =>
        token.Contains('~') ? GetKeyword(JsonPointerTokenizer.Unescape(token)) : GetKeyword(token);

    private static bool TryParseArrayIndex(ReadOnlySpan<char> token, out int index)
    {
        index = 0;

        //  RFC 6901 does not allow leading zeros.
        if (token.IsEmpty || (token.Length > 1 && token[0] == '0'))
        {
            return false;
        }

        foreach (var character in token)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return int.TryParse(token, out index);
    }

    private ref struct JsonPointerTokenizer(ReadOnlySpan<char> pointer)
    {
        private ReadOnlySpan<char> remaining = pointer;

        public bool TryReadToken(out ReadOnlySpan<char> token)
        {
            if (remaining.IsEmpty)
            {
                token = default;
                return false;
            }

            if (remaining[0] != '/')
            {
                throw new FormatException("A JSON Pointer must be empty or start with '/'.");
            }

            remaining = remaining[1..];
            var end = remaining.IndexOf('/');
            if (end < 0)
            {
                token = remaining;
                remaining = default;
            }
            else
            {
                token = remaining[..end];
                remaining = remaining[end..];
            }

            return true;
        }

        public static string Unescape(ReadOnlySpan<char> token)
        {
            if (!token.Contains('~'))
            {
                return token.ToString();
            }

            var builder = new StringBuilder(token.Length);
            for (var position = 0; position < token.Length; position++)
            {
                var character = token[position];
                if (character != '~')
                {
                    builder.Append(character);
                    continue;
                }

                position++;
                builder.Append(
                    position < token.Length
                        ? token[position] switch
                        {
                            '0' => '~',
                            '1' => '/',
                            _ => throw new FormatException("A JSON Pointer can only use '~0' and '~1' as escapes."),
                        }
                        : throw new FormatException("A JSON Pointer can only use '~0' and '~1' as escapes.")
                );
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// A buffer and a writer that one thread reuses for <see cref="ToJsonString"/>.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "The writer only writes to a managed buffer, and the instance lives as long as its thread."
    )]
    private sealed class PooledJsonWriter
    {
        //  Do not keep a buffer that a very large schema has grown.
        private const int MaxRetainedBytes = 64 * 1024;

        private readonly ArrayBufferWriter<byte> buffer = new(1024);
        private Utf8JsonWriter? writer;

        public ReadOnlySpan<byte> WrittenSpan => buffer.WrittenSpan;

        private static readonly JsonWriterOptions writerOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static bool CanReuse(JsonWriterOptions options) =>
            !options.Indented
            && ReferenceEquals(options.Encoder, writerOptions.Encoder)
            && !options.SkipValidation
            && options.MaxDepth == 0;

        public Utf8JsonWriter Start()
        {
            if (writer is null)
            {
                writer = new Utf8JsonWriter(buffer, writerOptions);
            }
            else
            {
                writer.Reset(buffer);
            }

            return writer;
        }

        public bool TryFinish()
        {
            var keep = buffer.Capacity <= MaxRetainedBytes;
            buffer.ResetWrittenCount();
            return keep;
        }
    }
}
