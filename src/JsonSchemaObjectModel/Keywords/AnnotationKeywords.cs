using System.Collections.Immutable;
using System.Text.Json;

namespace JsonSchemaObjectModel.Keywords;

/// <summary>The <c>title</c> keyword.</summary>
public sealed class TitleKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "title";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>description</c> keyword.</summary>
public sealed class DescriptionKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "description";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>default</c> keyword.</summary>
public sealed class DefaultKeyword : JsonValueKeyword
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "default";

    /// <summary>Creates the keyword.</summary>
    public DefaultKeyword(JsonElement value)
        : base(value) { }

    internal DefaultKeyword(PrimitiveJsonValue value)
        : base(value) { }

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>deprecated</c> keyword.</summary>
public sealed class DeprecatedKeyword(bool value) : BooleanValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "deprecated";

    private static readonly DeprecatedKeyword sharedTrue = new(true);
    private static readonly DeprecatedKeyword sharedFalse = new(false);

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static DeprecatedKeyword For(bool value) => value ? sharedTrue : sharedFalse;

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>readOnly</c> keyword.</summary>
public sealed class ReadOnlyKeyword(bool value) : BooleanValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "readOnly";

    private static readonly ReadOnlyKeyword sharedTrue = new(true);
    private static readonly ReadOnlyKeyword sharedFalse = new(false);

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static ReadOnlyKeyword For(bool value) => value ? sharedTrue : sharedFalse;

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>writeOnly</c> keyword.</summary>
public sealed class WriteOnlyKeyword(bool value) : BooleanValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "writeOnly";

    private static readonly WriteOnlyKeyword sharedTrue = new(true);
    private static readonly WriteOnlyKeyword sharedFalse = new(false);

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static WriteOnlyKeyword For(bool value) => value ? sharedTrue : sharedFalse;

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>examples</c> keyword.</summary>
public sealed class ExamplesKeyword : JsonValueArrayKeyword
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "examples";

    /// <summary>Creates the keyword.</summary>
    public ExamplesKeyword(IEnumerable<JsonElement> values)
        : base(values) { }

    internal ExamplesKeyword(ImmutableArray<JsonElement> clonedValues)
        : base(clonedValues) { }

    internal ExamplesKeyword(ImmutableArray<string> strings)
        : base(strings) { }

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>format</c> keyword. <see cref="JsonSchemaFormats"/> has the names that draft 2020-12 defines.</summary>
public sealed class FormatKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "format";

    /// <summary>
    /// One shared keyword for each format name that draft 2020-12 defines.
    /// </summary>
    internal static readonly FormatKeyword[] Standard =
    [
        new(JsonSchemaFormats.DateTime),
        new(JsonSchemaFormats.Date),
        new(JsonSchemaFormats.Time),
        new(JsonSchemaFormats.Duration),
        new(JsonSchemaFormats.Email),
        new(JsonSchemaFormats.IdnEmail),
        new(JsonSchemaFormats.Hostname),
        new(JsonSchemaFormats.IdnHostname),
        new(JsonSchemaFormats.Ipv4),
        new(JsonSchemaFormats.Ipv6),
        new(JsonSchemaFormats.Uri),
        new(JsonSchemaFormats.UriReference),
        new(JsonSchemaFormats.Iri),
        new(JsonSchemaFormats.IriReference),
        new(JsonSchemaFormats.Uuid),
        new(JsonSchemaFormats.UriTemplate),
        new(JsonSchemaFormats.JsonPointer),
        new(JsonSchemaFormats.RelativeJsonPointer),
        new(JsonSchemaFormats.Regex),
    ];

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static FormatKeyword For(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        foreach (var keyword in Standard)
        {
            if (string.Equals(keyword.Value, format, StringComparison.Ordinal))
            {
                return keyword;
            }
        }

        return new FormatKeyword(format);
    }

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>contentEncoding</c> keyword.</summary>
public sealed class ContentEncodingKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "contentEncoding";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>contentMediaType</c> keyword.</summary>
public sealed class ContentMediaTypeKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "contentMediaType";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>contentSchema</c> keyword.</summary>
public sealed class ContentSchemaKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "contentSchema";

    /// <inheritdoc/>
    public override string Name => KeywordName;

    /// <inheritdoc/>
    public override ContentSchemaKeyword WithSchema(JsonSchema schema) => ReferenceEquals(schema, Schema) ? this : new(schema);
}
