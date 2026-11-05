using System.Collections.ObjectModel;
using System.Text.Json;

namespace JsonSchemaObjectModel.Keywords;

/// <summary>The <c>$schema</c> keyword. It identifies the dialect of the schema.</summary>
public sealed class SchemaKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$schema";

    /// <summary>The URI of the draft 2020-12 meta-schema.</summary>
    public const string Draft202012 = "https://json-schema.org/draft/2020-12/schema";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>$id</c> keyword.</summary>
public sealed class IdKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$id";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>$ref</c> keyword.</summary>
public sealed class RefKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$ref";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>$anchor</c> keyword.</summary>
public sealed class AnchorKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$anchor";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>$dynamicRef</c> keyword.</summary>
public sealed class DynamicRefKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$dynamicRef";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>$dynamicAnchor</c> keyword.</summary>
public sealed class DynamicAnchorKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$dynamicAnchor";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>$comment</c> keyword.</summary>
public sealed class CommentKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$comment";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>$defs</c> keyword.</summary>
public sealed class DefsKeyword(IEnumerable<KeyValuePair<string, JsonSchema>> schemas) : SubschemaMapKeyword(schemas)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$defs";

    /// <inheritdoc/>
    public override string Name => KeywordName;

    /// <inheritdoc/>
    public override DefsKeyword WithSchemas(SchemaMap schemas) => ReferenceEquals(schemas, Schemas) ? this : new(schemas);
}

/// <summary>
/// The <c>$vocabulary</c> keyword. A meta-schema uses it to declare the vocabularies of its dialect.
/// </summary>
public sealed class VocabularyKeyword : IJsonSchemaKeyword, IKeywordValueEquality
{
    bool IKeywordValueEquality.ValueEquals(IJsonSchemaKeyword other)
    {
        var otherVocabularies = ((VocabularyKeyword)other).Vocabularies;
        if (otherVocabularies.Count != Vocabularies.Count)
        {
            return false;
        }

        foreach (var (uri, required) in Vocabularies)
        {
            if (!otherVocabularies.TryGetValue(uri, out var otherRequired) || required != otherRequired)
            {
                return false;
            }
        }

        return true;
    }

    int IKeywordValueEquality.GetValueHashCode()
    {
        var sum = 0;
        foreach (var (uri, required) in Vocabularies)
        {
            sum = unchecked(sum + JsonValueEquality.Property(JsonValueEquality.String(uri), JsonValueEquality.Boolean(required)));
        }

        return JsonValueEquality.ObjectHash(sum, Vocabularies.Count);
    }

    /// <summary>The keyword name.</summary>
    public const string KeywordName = "$vocabulary";

    /// <summary>
    /// Creates the keyword. The order of <paramref name="vocabularies"/> is kept.
    /// </summary>
    /// <param name="vocabularies">
    /// Vocabulary URIs with a flag.
    /// The flag is <see langword="true"/> if an implementation must know the vocabulary to process the schema.
    /// </param>
    public VocabularyKeyword(IEnumerable<KeyValuePair<string, bool>> vocabularies)
    {
        ArgumentNullException.ThrowIfNull(vocabularies);
        var ordered = new OrderedDictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (uri, required) in vocabularies)
        {
            if (!ordered.TryAdd(uri, required))
            {
                throw new ArgumentException($"The vocabulary '{uri}' occurs more than once.", nameof(vocabularies));
            }
        }

        Vocabularies = new ReadOnlyDictionary<string, bool>(ordered);
    }

    /// <inheritdoc/>
    public string Name => KeywordName;

    /// <summary>
    /// The vocabulary URIs and their required flags, in document order.
    /// </summary>
    public IReadOnlyDictionary<string, bool> Vocabularies { get; }

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (uri, required) in Vocabularies)
        {
            writer.WriteBoolean(uri, required);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// A keyword with no registered reader. It keeps the value as JSON, so the schema round-trips.
/// </summary>
public sealed class UnknownKeyword : IJsonSchemaKeyword, IKeywordValueEquality
{
    bool IKeywordValueEquality.ValueEquals(IJsonSchemaKeyword other) =>
        JsonValueEquality.ElementEquals(Value, ((UnknownKeyword)other).Value);

    int IKeywordValueEquality.GetValueHashCode() =>
        JsonValueEquality.Element(Value);

    /// <summary>
    /// Creates the keyword.
    /// </summary>
    public UnknownKeyword(string name, JsonElement value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Value = value.Clone();
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>
    /// The keyword value.
    /// </summary>
    public JsonElement Value { get; }

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer) => Value.WriteTo(writer);
}
