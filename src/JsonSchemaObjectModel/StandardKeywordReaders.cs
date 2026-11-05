using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel;

public readonly partial struct JsonSchemaReadContext
{
    /// <summary>
    /// Reads a string value.
    /// </summary>
    public string ReadString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Error("The value must be a string.");

    /// <summary>
    /// Reads a boolean value.
    /// </summary>
    public bool ReadBoolean(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Error("The value must be a boolean."),
        };

    /// <summary>
    /// Reads a number value. The result is a clone that does not depend on the source document.
    /// </summary>
    public JsonElement ReadNumber(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number ? value.Clone() : throw Error("The value must be a number.");

    /// <summary>
    /// Reads a non-negative integer. A number with a zero fractional part, for example <c>2.0</c>, is an integer.
    /// </summary>
    public ulong ReadNonNegativeInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw Error("The value must be a non-negative integer.");
        }

        if (value.TryGetUInt64(out var integer))
        {
            return integer;
        }

        if (value.TryGetDecimal(out var number) && number >= 0 && number <= ulong.MaxValue && decimal.Truncate(number) == number)
        {
            return (ulong)number;
        }

        throw Error("The value must be a non-negative integer.");
    }

    /// <summary>
    /// Reads an array of subschemas.
    /// </summary>
    public ImmutableArray<JsonSchema> ReadSchemaArray(JsonElement value, bool allowEmpty = false)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw Error("The value must be an array of schemas.");
        }

        var length = value.GetArrayLength();
        if (length == 0 && !allowEmpty)
        {
            throw Error("The array must contain at least one schema.");
        }

        var schemas = new JsonSchema[length];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            schemas[index] = ReadSchema(item, index);
            index++;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(schemas);
    }

    /// <summary>
    /// Reads an object of named subschemas, in document order.
    /// </summary>
    /// <remarks>
    /// Pass the result to a keyword that derives from <see cref="SubschemaMapKeyword"/>. The keyword uses it without a copy.
    /// </remarks>
    public IReadOnlyDictionary<string, JsonSchema> ReadSchemaMap(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Error("The value must be an object of schemas.");
        }

        var schemas = new OwnedSchemaMap(value.GetPropertyCount());
        foreach (var property in value.EnumerateObject())
        {
            var name = property.Name;
            if (!schemas.TryAdd(name, ReadSchema(property.Value, name)))
            {
                throw Error($"The name '{name}' occurs more than once.");
            }
        }

        return schemas;
    }

    /// <summary>
    /// Reads an array of unique strings.
    /// </summary>
    public ImmutableArray<string> ReadUniqueStringArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw Error("The value must be an array of strings.");
        }

        var strings = new string[value.GetArrayLength()];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw Error("The value must be an array of strings.");
            }

            var text = item.GetString()!;
            if (Array.IndexOf(strings, text, 0, index) >= 0)
            {
                throw Error($"The string '{text}' occurs more than once.");
            }

            strings[index++] = text;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(strings);
    }

    /// <summary>
    /// Reads an array of JSON values. The results are clones that do not depend on the source document.
    /// </summary>
    public ImmutableArray<JsonElement> ReadValueArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw Error("The value must be an array.");
        }

        var values = new JsonElement[value.GetArrayLength()];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            values[index++] = item.Clone();
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(values);
    }
}

internal static class StandardKeywordReaders
{
    public static void AddDraft202012(JsonSchemaKeywordRegistry registry)
    {
        //  Core vocabulary.
        registry
            .Add(SchemaKeyword.KeywordName, (value, context) => new SchemaKeyword(context.ReadString(value)))
            .Add(IdKeyword.KeywordName, (value, context) => new IdKeyword(context.ReadString(value)))
            .Add(RefKeyword.KeywordName, (value, context) => new RefKeyword(context.ReadString(value)))
            .Add(AnchorKeyword.KeywordName, (value, context) => new AnchorKeyword(context.ReadString(value)))
            .Add(DynamicRefKeyword.KeywordName, (value, context) => new DynamicRefKeyword(context.ReadString(value)))
            .Add(DynamicAnchorKeyword.KeywordName, (value, context) => new DynamicAnchorKeyword(context.ReadString(value)))
            .Add(CommentKeyword.KeywordName, (value, context) => new CommentKeyword(context.ReadString(value)))
            .Add(DefsKeyword.KeywordName, (value, context) => new DefsKeyword(context.ReadSchemaMap(value)))
            .Add(VocabularyKeyword.KeywordName, ReadVocabulary);

        //  Applicator vocabulary.
        registry
            .Add(AllOfKeyword.KeywordName, (value, context) => new AllOfKeyword(context.ReadSchemaArray(value)))
            .Add(AnyOfKeyword.KeywordName, (value, context) => new AnyOfKeyword(context.ReadSchemaArray(value)))
            .Add(OneOfKeyword.KeywordName, (value, context) => new OneOfKeyword(context.ReadSchemaArray(value)))
            .Add(NotKeyword.KeywordName, (value, context) => new NotKeyword(context.ReadSchema(value)))
            .Add(IfKeyword.KeywordName, (value, context) => new IfKeyword(context.ReadSchema(value)))
            .Add(ThenKeyword.KeywordName, (value, context) => new ThenKeyword(context.ReadSchema(value)))
            .Add(ElseKeyword.KeywordName, (value, context) => new ElseKeyword(context.ReadSchema(value)))
            .Add(
                DependentSchemasKeyword.KeywordName,
                (value, context) => new DependentSchemasKeyword(context.ReadSchemaMap(value))
            )
            .Add(PrefixItemsKeyword.KeywordName, (value, context) => new PrefixItemsKeyword(context.ReadSchemaArray(value)))
            .Add(ItemsKeyword.KeywordName, (value, context) => new ItemsKeyword(context.ReadSchema(value)))
            .Add(ContainsKeyword.KeywordName, (value, context) => new ContainsKeyword(context.ReadSchema(value)))
            .Add(PropertiesKeyword.KeywordName, (value, context) => new PropertiesKeyword(context.ReadSchemaMap(value)))
            .Add(
                PatternPropertiesKeyword.KeywordName,
                (value, context) => new PatternPropertiesKeyword(context.ReadSchemaMap(value))
            )
            .Add(
                AdditionalPropertiesKeyword.KeywordName,
                (value, context) => new AdditionalPropertiesKeyword(context.ReadSchema(value))
            )
            .Add(PropertyNamesKeyword.KeywordName, (value, context) => new PropertyNamesKeyword(context.ReadSchema(value)));

        //  Unevaluated vocabulary.
        registry
            .Add(
                UnevaluatedItemsKeyword.KeywordName,
                (value, context) => new UnevaluatedItemsKeyword(context.ReadSchema(value))
            )
            .Add(
                UnevaluatedPropertiesKeyword.KeywordName,
                (value, context) => new UnevaluatedPropertiesKeyword(context.ReadSchema(value))
            );

        //  Validation vocabulary.
        registry
            .Add(TypeKeyword.KeywordName, ReadType)
            .Add(EnumKeyword.KeywordName, (value, context) => new EnumKeyword(context.ReadValueArray(value)))
            .Add(ConstKeyword.KeywordName, (value, _) => new ConstKeyword(value))
            .Add(MultipleOfKeyword.KeywordName, ReadMultipleOf)
            .Add(MaximumKeyword.KeywordName, (value, context) => new MaximumKeyword(context.ReadNumber(value)))
            .Add(
                ExclusiveMaximumKeyword.KeywordName,
                (value, context) => new ExclusiveMaximumKeyword(context.ReadNumber(value))
            )
            .Add(MinimumKeyword.KeywordName, (value, context) => new MinimumKeyword(context.ReadNumber(value)))
            .Add(
                ExclusiveMinimumKeyword.KeywordName,
                (value, context) => new ExclusiveMinimumKeyword(context.ReadNumber(value))
            )
            .Add(MaxLengthKeyword.KeywordName, (value, context) => new MaxLengthKeyword(context.ReadNonNegativeInteger(value)))
            .Add(MinLengthKeyword.KeywordName, (value, context) => new MinLengthKeyword(context.ReadNonNegativeInteger(value)))
            .Add(PatternKeyword.KeywordName, (value, context) => new PatternKeyword(context.ReadString(value)))
            .Add(MaxItemsKeyword.KeywordName, (value, context) => new MaxItemsKeyword(context.ReadNonNegativeInteger(value)))
            .Add(MinItemsKeyword.KeywordName, (value, context) => new MinItemsKeyword(context.ReadNonNegativeInteger(value)))
            .Add(UniqueItemsKeyword.KeywordName, (value, context) => new UniqueItemsKeyword(context.ReadBoolean(value)))
            .Add(
                MaxContainsKeyword.KeywordName,
                (value, context) => new MaxContainsKeyword(context.ReadNonNegativeInteger(value))
            )
            .Add(
                MinContainsKeyword.KeywordName,
                (value, context) => new MinContainsKeyword(context.ReadNonNegativeInteger(value))
            )
            .Add(
                MaxPropertiesKeyword.KeywordName,
                (value, context) => new MaxPropertiesKeyword(context.ReadNonNegativeInteger(value))
            )
            .Add(
                MinPropertiesKeyword.KeywordName,
                (value, context) => new MinPropertiesKeyword(context.ReadNonNegativeInteger(value))
            )
            .Add(RequiredKeyword.KeywordName, (value, context) => new RequiredKeyword(context.ReadUniqueStringArray(value)))
            .Add(DependentRequiredKeyword.KeywordName, ReadDependentRequired);

        //  Meta-data vocabulary.
        registry
            .Add(TitleKeyword.KeywordName, (value, context) => new TitleKeyword(context.ReadString(value)))
            .Add(DescriptionKeyword.KeywordName, (value, context) => new DescriptionKeyword(context.ReadString(value)))
            .Add(DefaultKeyword.KeywordName, (value, _) => new DefaultKeyword(value))
            .Add(DeprecatedKeyword.KeywordName, (value, context) => new DeprecatedKeyword(context.ReadBoolean(value)))
            .Add(ReadOnlyKeyword.KeywordName, (value, context) => new ReadOnlyKeyword(context.ReadBoolean(value)))
            .Add(WriteOnlyKeyword.KeywordName, (value, context) => new WriteOnlyKeyword(context.ReadBoolean(value)))
            .Add(ExamplesKeyword.KeywordName, (value, context) => new ExamplesKeyword(context.ReadValueArray(value)));

        //  Format-annotation and content vocabularies.
        registry
            .Add(FormatKeyword.KeywordName, ReadFormat)
            .Add(ContentEncodingKeyword.KeywordName, (value, context) => new ContentEncodingKeyword(context.ReadString(value)))
            .Add(
                ContentMediaTypeKeyword.KeywordName,
                (value, context) => new ContentMediaTypeKeyword(context.ReadString(value))
            )
            .Add(ContentSchemaKeyword.KeywordName, (value, context) => new ContentSchemaKeyword(context.ReadSchema(value)));
    }

    private static TypeKeyword ReadType(JsonElement value, JsonSchemaReadContext context)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return TypeKeyword.TryParseTypeName(value, out var single)
                ? TypeKeyword.For(single)
                : throw context.Error($"'{value.GetString()}' is not a JSON Schema type.");
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw context.Error("The value must be a string or an array of strings.");
        }

        var types = new SchemaValueType[value.GetArrayLength()];
        var index = 0;
        var seen = SchemaValueType.None;
        foreach (var item in value.EnumerateArray())
        {
            if (!TypeKeyword.TryParseTypeName(item, out var type))
            {
                throw context.Error($"'{item}' is not a JSON Schema type.");
            }

            if ((seen & type) != 0)
            {
                throw context.Error($"The type '{item.GetString()}' occurs more than once.");
            }

            seen |= type;
            types[index++] = type;
        }

        return new TypeKeyword(ImmutableCollectionsMarshal.AsImmutableArray(types));
    }

    private static readonly string[] knownFormats =
    [
        Formats.DateTime,
        Formats.Date,
        Formats.Time,
        Formats.Duration,
        Formats.Email,
        Formats.IdnEmail,
        Formats.Hostname,
        Formats.IdnHostname,
        Formats.Ipv4,
        Formats.Ipv6,
        Formats.Uri,
        Formats.UriReference,
        Formats.Iri,
        Formats.IriReference,
        Formats.Uuid,
        Formats.UriTemplate,
        Formats.JsonPointer,
        Formats.RelativeJsonPointer,
        Formats.Regex,
    ];

    /// <summary>
    /// Reads <c>format</c>. A standard format name reuses the constant string instead of allocating one.
    /// </summary>
    private static FormatKeyword ReadFormat(JsonElement value, JsonSchemaReadContext context)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw context.Error("The value must be a string.");
        }

        foreach (var format in knownFormats)
        {
            if (value.ValueEquals(format))
            {
                return new FormatKeyword(format);
            }
        }

        return new FormatKeyword(value.GetString()!);
    }

    private static MultipleOfKeyword ReadMultipleOf(JsonElement value, JsonSchemaReadContext context)
    {
        var number = context.ReadNumber(value);
        return number.GetDouble() > 0 ? new MultipleOfKeyword(number) : throw context.Error("The value must be greater than zero.");
    }

    private static VocabularyKeyword ReadVocabulary(JsonElement value, JsonSchemaReadContext context)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw context.Error("The value must be an object of vocabulary URIs to booleans.");
        }

        var vocabularies = new List<KeyValuePair<string, bool>>(value.GetPropertyCount());
        foreach (var property in value.EnumerateObject())
        {
            vocabularies.Add(new(property.Name, context.ReadBoolean(property.Value)));
        }

        return new VocabularyKeyword(vocabularies);
    }

    private static DependentRequiredKeyword ReadDependentRequired(JsonElement value, JsonSchemaReadContext context)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw context.Error("The value must be an object of string arrays.");
        }

        var dependencies = new List<KeyValuePair<string, IEnumerable<string>>>(value.GetPropertyCount());
        foreach (var property in value.EnumerateObject())
        {
            dependencies.Add(new(property.Name, context.ReadUniqueStringArray(property.Value)));
        }

        return new DependentRequiredKeyword(dependencies);
    }
}
