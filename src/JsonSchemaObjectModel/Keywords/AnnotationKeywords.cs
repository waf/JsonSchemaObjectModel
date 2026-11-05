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
public sealed class DefaultKeyword(JsonElement value) : JsonValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "default";

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

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>format</c> keyword. <see cref="Formats"/> has the names that draft 2020-12 defines.</summary>
public sealed class FormatKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "format";

    /// <summary>
    /// One shared keyword for each format name that draft 2020-12 defines.
    /// </summary>
    internal static readonly FormatKeyword[] Standard =
    [
        new(Formats.DateTime),
        new(Formats.Date),
        new(Formats.Time),
        new(Formats.Duration),
        new(Formats.Email),
        new(Formats.IdnEmail),
        new(Formats.Hostname),
        new(Formats.IdnHostname),
        new(Formats.Ipv4),
        new(Formats.Ipv6),
        new(Formats.Uri),
        new(Formats.UriReference),
        new(Formats.Iri),
        new(Formats.IriReference),
        new(Formats.Uuid),
        new(Formats.UriTemplate),
        new(Formats.JsonPointer),
        new(Formats.RelativeJsonPointer),
        new(Formats.Regex),
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
}
