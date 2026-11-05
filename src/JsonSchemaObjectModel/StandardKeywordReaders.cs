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
    /// Reads a number value. The result does not depend on the source document.
    /// </summary>
    public JsonNumber ReadNumber(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number ? JsonNumber.FromElement(value) : throw Error("The value must be a number.");

    /// <summary>
    /// Reads a non-negative integer. A number with a zero fractional part, for example <c>2.0</c> or <c>1e2</c>, is an integer.
    /// </summary>
    /// <remarks>
    /// The value must not be more than <see cref="ulong.MaxValue"/>. JSON Schema has no upper limit, but no real
    /// length or count comes near this value.
    /// </remarks>
    public ulong ReadNonNegativeInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw Error("The value must be a non-negative integer.");
        }

        var parts = JsonNumberParts.From(value);
        if (parts.IsNegative || !parts.IsInteger)
        {
            throw Error("The value must be a non-negative integer.");
        }

        return parts.TryGetUInt64(out var integer)
            ? integer
            : throw Error($"The value is more than {ulong.MaxValue}, the largest non-negative integer that this library supports.");
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
    /// A keyword that derives from <see cref="SubschemaMapKeyword"/> uses the result without a copy.
    /// </remarks>
    public SchemaMap ReadSchemaMap(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Error("The value must be an object of schemas.");
        }

        var count = value.GetPropertyCount();
        var entries = new KeyValuePair<string, JsonSchema>[count];

        //  A large map gets its index here. The index also finds a duplicate name, so the map need not search again.
        var index = SchemaMap.NeedsIndex(count) ? new Dictionary<string, int>(count, StringComparer.Ordinal) : null;
        var position = 0;
        foreach (var property in value.EnumerateObject())
        {
            var name = property.Name;
            if (index is null ? SchemaMap.ContainsName(entries.AsSpan(0, position), name) : !index.TryAdd(name, position))
            {
                var duplicate = (SchemaReadException)Error($"The name '{name}' occurs more than once.");
                duplicate.AddParent(name);
                throw duplicate;
            }

            entries[position++] = new(name, ReadSchema(property.Value, name));
        }

        return SchemaMap.FromUniqueEntries(entries, index);
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
            .Add(NotKeyword.KeywordName, (value, context) => NotKeyword.For(context.ReadSchema(value)))
            .Add(IfKeyword.KeywordName, (value, context) => new IfKeyword(context.ReadSchema(value)))
            .Add(ThenKeyword.KeywordName, (value, context) => new ThenKeyword(context.ReadSchema(value)))
            .Add(ElseKeyword.KeywordName, (value, context) => new ElseKeyword(context.ReadSchema(value)))
            .Add(
                DependentSchemasKeyword.KeywordName,
                (value, context) => new DependentSchemasKeyword(context.ReadSchemaMap(value))
            )
            .Add(PrefixItemsKeyword.KeywordName, (value, context) => new PrefixItemsKeyword(context.ReadSchemaArray(value)))
            .Add(ItemsKeyword.KeywordName, (value, context) => ItemsKeyword.For(context.ReadSchema(value)))
            .Add(ContainsKeyword.KeywordName, (value, context) => ContainsKeyword.For(context.ReadSchema(value)))
            .Add(PropertiesKeyword.KeywordName, (value, context) => new PropertiesKeyword(context.ReadSchemaMap(value)))
            .Add(
                PatternPropertiesKeyword.KeywordName,
                (value, context) => new PatternPropertiesKeyword(context.ReadSchemaMap(value))
            )
            .Add(
                AdditionalPropertiesKeyword.KeywordName,
                (value, context) => AdditionalPropertiesKeyword.For(context.ReadSchema(value))
            )
            .Add(PropertyNamesKeyword.KeywordName, (value, context) => PropertyNamesKeyword.For(context.ReadSchema(value)));

        //  Unevaluated vocabulary.
        registry
            .Add(
                UnevaluatedItemsKeyword.KeywordName,
                (value, context) => UnevaluatedItemsKeyword.For(context.ReadSchema(value))
            )
            .Add(
                UnevaluatedPropertiesKeyword.KeywordName,
                (value, context) => UnevaluatedPropertiesKeyword.For(context.ReadSchema(value))
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
            .Add(MaxLengthKeyword.KeywordName, (value, context) => MaxLengthKeyword.For(context.ReadNonNegativeInteger(value)))
            .Add(MinLengthKeyword.KeywordName, (value, context) => MinLengthKeyword.For(context.ReadNonNegativeInteger(value)))
            .Add(PatternKeyword.KeywordName, (value, context) => new PatternKeyword(context.ReadString(value)))
            .Add(MaxItemsKeyword.KeywordName, (value, context) => MaxItemsKeyword.For(context.ReadNonNegativeInteger(value)))
            .Add(MinItemsKeyword.KeywordName, (value, context) => MinItemsKeyword.For(context.ReadNonNegativeInteger(value)))
            .Add(UniqueItemsKeyword.KeywordName, (value, context) => UniqueItemsKeyword.For(context.ReadBoolean(value)))
            .Add(
                MaxContainsKeyword.KeywordName,
                (value, context) => MaxContainsKeyword.For(context.ReadNonNegativeInteger(value))
            )
            .Add(
                MinContainsKeyword.KeywordName,
                (value, context) => MinContainsKeyword.For(context.ReadNonNegativeInteger(value))
            )
            .Add(
                MaxPropertiesKeyword.KeywordName,
                (value, context) => MaxPropertiesKeyword.For(context.ReadNonNegativeInteger(value))
            )
            .Add(
                MinPropertiesKeyword.KeywordName,
                (value, context) => MinPropertiesKeyword.For(context.ReadNonNegativeInteger(value))
            )
            .Add(RequiredKeyword.KeywordName, (value, context) => new RequiredKeyword(context.ReadUniqueStringArray(value)))
            .Add(DependentRequiredKeyword.KeywordName, ReadDependentRequired);

        //  Meta-data vocabulary.
        registry
            .Add(TitleKeyword.KeywordName, (value, context) => new TitleKeyword(context.ReadString(value)))
            .Add(DescriptionKeyword.KeywordName, (value, context) => new DescriptionKeyword(context.ReadString(value)))
            .Add(DefaultKeyword.KeywordName, (value, _) => new DefaultKeyword(value))
            .Add(DeprecatedKeyword.KeywordName, (value, context) => DeprecatedKeyword.For(context.ReadBoolean(value)))
            .Add(ReadOnlyKeyword.KeywordName, (value, context) => ReadOnlyKeyword.For(context.ReadBoolean(value)))
            .Add(WriteOnlyKeyword.KeywordName, (value, context) => WriteOnlyKeyword.For(context.ReadBoolean(value)))
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

        var length = value.GetArrayLength();
        if (length == 0)
        {
            throw context.Error("The array must contain at least one type.");
        }

        var types = new SchemaValueType[length];
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

    /// <summary>
    /// Reads <c>format</c>. A standard format name gives the shared keyword, so it allocates nothing.
    /// </summary>
    private static FormatKeyword ReadFormat(JsonElement value, JsonSchemaReadContext context)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw context.Error("The value must be a string.");
        }

        foreach (var keyword in FormatKeyword.Standard)
        {
            if (value.ValueEquals(keyword.Value))
            {
                return keyword;
            }
        }

        return new FormatKeyword(value.GetString()!);
    }

    private static MultipleOfKeyword ReadMultipleOf(JsonElement value, JsonSchemaReadContext context)
    {
        //  The sign comes from the text. A double would make a very small value such as 1e-400 into zero.
        var number = context.ReadNumber(value);
        return number.Sign > 0
            ? new MultipleOfKeyword(number)
            : throw context.Error("The value must be greater than zero.");
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
