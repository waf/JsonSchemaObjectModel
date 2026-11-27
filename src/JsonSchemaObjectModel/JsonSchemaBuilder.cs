using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel;

/// <summary>
/// Builds a <see cref="JsonSchema"/> one keyword at a time.
/// </summary>
/// <remarks>
/// - Each keyword method adds a keyword. A keyword with the same name replaces the earlier one and keeps its position.
/// - The builder converts to <see cref="JsonSchema"/> implicitly, so you can pass it where a schema is expected.
/// - Add a custom keyword with <see cref="Add"/>, or replace a keyword with <see cref="Set"/>.
/// </remarks>
public sealed class JsonSchemaBuilder
{
    private readonly List<IJsonSchemaKeyword> keywords = [];

    //  Shortcuts for a builder that starts with "type". They have the names of the JSON types,
    //  so they cannot clash with the keyword methods, which have the names of keywords.

    /// <summary>Creates a builder with <c>"type": "string"</c>.</summary>
    public static JsonSchemaBuilder String() => new JsonSchemaBuilder().Type(JsonSchemaType.String);

    /// <summary>Creates a builder with <c>"type": "number"</c>.</summary>
    public static JsonSchemaBuilder Number() => new JsonSchemaBuilder().Type(JsonSchemaType.Number);

    /// <summary>Creates a builder with <c>"type": "integer"</c>.</summary>
    public static JsonSchemaBuilder Integer() => new JsonSchemaBuilder().Type(JsonSchemaType.Integer);

    /// <summary>Creates a builder with <c>"type": "boolean"</c>.</summary>
    public static JsonSchemaBuilder Boolean() => new JsonSchemaBuilder().Type(JsonSchemaType.Boolean);

    /// <summary>Creates a builder with <c>"type": "object"</c>.</summary>
    public static JsonSchemaBuilder Object() => new JsonSchemaBuilder().Type(JsonSchemaType.Object);

    /// <summary>Creates a builder with <c>"type": "array"</c>.</summary>
    public static JsonSchemaBuilder Array() => new JsonSchemaBuilder().Type(JsonSchemaType.Array);

    /// <summary>Creates a builder with <c>"type": "null"</c>.</summary>
    public static JsonSchemaBuilder Null() => new JsonSchemaBuilder().Type(JsonSchemaType.Null);

    /// <summary>
    /// Converts the builder to a schema.
    /// </summary>
    public static implicit operator JsonSchema(JsonSchemaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a schema from the keywords added so far.
    /// You can continue to use the builder after this call.
    /// </summary>
    public JsonSchema Build() => keywords.Count == 0 ? JsonSchema.Empty : new JsonSchema([.. keywords]);

    /// <summary>
    /// Adds a keyword.
    /// </summary>
    /// <remarks>
    /// The typed accessors, for example <c>schema.Type</c>, only see typed keywords.
    /// An <see cref="UntypedKeyword"/> with a standard name is kept and written, but those accessors do not see it.
    /// </remarks>
    /// <exception cref="ArgumentException">The builder already has a keyword with the same name. Use <see cref="Set"/> to replace it.</exception>
    public JsonSchemaBuilder Add(IJsonSchemaKeyword keyword)
    {
        ArgumentNullException.ThrowIfNull(keyword);
        if (IndexOf(keyword.Name) >= 0)
        {
            throw new ArgumentException($"The builder already has the keyword '{keyword.Name}'.", nameof(keyword));
        }

        keywords.Add(keyword);
        return this;
    }

    /// <summary>
    /// Adds a keyword, or replaces the keyword that has the same name. A replacement keeps the position of the earlier keyword.
    /// </summary>
    /// <remarks>
    /// The typed accessors, for example <c>schema.Type</c>, only see typed keywords.
    /// An <see cref="UntypedKeyword"/> with a standard name is kept and written, but those accessors do not see it.
    /// </remarks>
    public JsonSchemaBuilder Set(IJsonSchemaKeyword keyword)
    {
        ArgumentNullException.ThrowIfNull(keyword);
        var position = IndexOf(keyword.Name);
        if (position >= 0)
        {
            keywords[position] = keyword;
        }
        else
        {
            keywords.Add(keyword);
        }

        return this;
    }

    /// <summary>
    /// Removes the keyword with the given name, if the builder has it.
    /// </summary>
    public JsonSchemaBuilder Remove(string name)
    {
        keywords.RemoveAll(keyword => string.Equals(keyword.Name, name, StringComparison.Ordinal));
        return this;
    }

    //  Core vocabulary.

    /// <summary>Adds <c>$schema</c>. The value is the URI of the meta-schema that defines the dialect.</summary>
    public JsonSchemaBuilder SchemaDialect(string uri) => Set(new SchemaKeyword(uri));

    /// <summary>Adds <c>$id</c>.</summary>
    public JsonSchemaBuilder Id(string uri) => Set(new IdKeyword(uri));

    /// <summary>Adds <c>$ref</c>.</summary>
    public JsonSchemaBuilder Ref(string uri) => Set(new RefKeyword(uri));

    /// <summary>Adds <c>$anchor</c>.</summary>
    public JsonSchemaBuilder Anchor(string name) => Set(new AnchorKeyword(name));

    /// <summary>Adds <c>$dynamicRef</c>.</summary>
    public JsonSchemaBuilder DynamicRef(string uri) => Set(new DynamicRefKeyword(uri));

    /// <summary>Adds <c>$dynamicAnchor</c>.</summary>
    public JsonSchemaBuilder DynamicAnchor(string name) => Set(new DynamicAnchorKeyword(name));

    /// <summary>Adds <c>$comment</c>.</summary>
    public JsonSchemaBuilder Comment(string comment) => Set(new CommentKeyword(comment));

    /// <summary>Adds <c>$vocabulary</c>.</summary>
    public JsonSchemaBuilder Vocabulary(params ReadOnlySpan<(string Uri, bool Required)> vocabularies)
    {
        var pairs = new KeyValuePair<string, bool>[vocabularies.Length];
        for (var index = 0; index < pairs.Length; index++)
        {
            pairs[index] = KeyValuePair.Create(vocabularies[index].Uri, vocabularies[index].Required);
        }

        return Set(new VocabularyKeyword(pairs));
    }

    /// <summary>Adds <c>$vocabulary</c>.</summary>
    public JsonSchemaBuilder Vocabulary(IEnumerable<(string Uri, bool Required)> vocabularies)
    {
        ArgumentNullException.ThrowIfNull(vocabularies);
        return Set(new VocabularyKeyword(vocabularies.Select(vocabulary => KeyValuePair.Create(vocabulary.Uri, vocabulary.Required))));
    }

    /// <summary>Adds <c>$vocabulary</c>.</summary>
    public JsonSchemaBuilder Vocabulary(IEnumerable<KeyValuePair<string, bool>> vocabularies) =>
        Set(new VocabularyKeyword(vocabularies));

    /// <summary>Adds <c>$defs</c>.</summary>
    public JsonSchemaBuilder Defs(params ReadOnlySpan<(string Name, JsonSchema Schema)> definitions) =>
        Set(new DefsKeyword(ToMap(definitions)));

    /// <summary>Adds <c>$defs</c>.</summary>
    public JsonSchemaBuilder Defs(IEnumerable<(string Name, JsonSchema Schema)> definitions) =>
        Set(new DefsKeyword(ToPairs(definitions)));

    /// <summary>Adds <c>$defs</c>.</summary>
    public JsonSchemaBuilder Defs(IEnumerable<KeyValuePair<string, JsonSchema>> definitions) =>
        Set(new DefsKeyword(definitions));

    //  Applicator vocabulary.

    /// <summary>Adds <c>allOf</c>.</summary>
    public JsonSchemaBuilder AllOf(params ReadOnlySpan<JsonSchema> schemas) => Set(new AllOfKeyword(ImmutableArray.Create(schemas)));

    /// <summary>Adds <c>allOf</c>.</summary>
    public JsonSchemaBuilder AllOf(IEnumerable<JsonSchema> schemas) => Set(new AllOfKeyword(schemas));

    /// <summary>Adds <c>anyOf</c>.</summary>
    public JsonSchemaBuilder AnyOf(params ReadOnlySpan<JsonSchema> schemas) => Set(new AnyOfKeyword(ImmutableArray.Create(schemas)));

    /// <summary>Adds <c>anyOf</c>.</summary>
    public JsonSchemaBuilder AnyOf(IEnumerable<JsonSchema> schemas) => Set(new AnyOfKeyword(schemas));

    /// <summary>Adds <c>oneOf</c>.</summary>
    public JsonSchemaBuilder OneOf(params ReadOnlySpan<JsonSchema> schemas) => Set(new OneOfKeyword(ImmutableArray.Create(schemas)));

    /// <summary>Adds <c>oneOf</c>.</summary>
    public JsonSchemaBuilder OneOf(IEnumerable<JsonSchema> schemas) => Set(new OneOfKeyword(schemas));

    /// <summary>Adds <c>not</c>.</summary>
    public JsonSchemaBuilder Not(JsonSchema schema) => Set(NotKeyword.For(schema));

    /// <summary>Adds <c>if</c>.</summary>
    public JsonSchemaBuilder If(JsonSchema schema) => Set(new IfKeyword(schema));

    /// <summary>Adds <c>then</c>.</summary>
    public JsonSchemaBuilder Then(JsonSchema schema) => Set(new ThenKeyword(schema));

    /// <summary>Adds <c>else</c>.</summary>
    public JsonSchemaBuilder Else(JsonSchema schema) => Set(new ElseKeyword(schema));

    /// <summary>Adds <c>dependentSchemas</c>.</summary>
    public JsonSchemaBuilder DependentSchemas(params ReadOnlySpan<(string Name, JsonSchema Schema)> schemas) =>
        Set(new DependentSchemasKeyword(ToMap(schemas)));

    /// <summary>Adds <c>dependentSchemas</c>.</summary>
    public JsonSchemaBuilder DependentSchemas(IEnumerable<(string Name, JsonSchema Schema)> schemas) =>
        Set(new DependentSchemasKeyword(ToPairs(schemas)));

    /// <summary>Adds <c>dependentSchemas</c>.</summary>
    public JsonSchemaBuilder DependentSchemas(IEnumerable<KeyValuePair<string, JsonSchema>> schemas) =>
        Set(new DependentSchemasKeyword(schemas));

    /// <summary>Adds <c>prefixItems</c>.</summary>
    public JsonSchemaBuilder PrefixItems(params ReadOnlySpan<JsonSchema> schemas) => Set(new PrefixItemsKeyword(ImmutableArray.Create(schemas)));

    /// <summary>Adds <c>prefixItems</c>.</summary>
    public JsonSchemaBuilder PrefixItems(IEnumerable<JsonSchema> schemas) => Set(new PrefixItemsKeyword(schemas));

    /// <summary>Adds <c>items</c>.</summary>
    public JsonSchemaBuilder Items(JsonSchema schema) => Set(ItemsKeyword.For(schema));

    /// <summary>Adds <c>contains</c>.</summary>
    public JsonSchemaBuilder Contains(JsonSchema schema) => Set(ContainsKeyword.For(schema));

    /// <summary>Adds <c>properties</c>.</summary>
    public JsonSchemaBuilder Properties(params ReadOnlySpan<(string Name, JsonSchema Schema)> properties) =>
        Set(new PropertiesKeyword(ToMap(properties)));

    /// <summary>Adds <c>properties</c>.</summary>
    public JsonSchemaBuilder Properties(IEnumerable<(string Name, JsonSchema Schema)> properties) =>
        Set(new PropertiesKeyword(ToPairs(properties)));

    /// <summary>Adds <c>properties</c>.</summary>
    public JsonSchemaBuilder Properties(IEnumerable<KeyValuePair<string, JsonSchema>> properties) =>
        Set(new PropertiesKeyword(properties));

    /// <summary>Adds <c>patternProperties</c>.</summary>
    public JsonSchemaBuilder PatternProperties(params ReadOnlySpan<(string Pattern, JsonSchema Schema)> properties) =>
        Set(new PatternPropertiesKeyword(ToMap(properties)));

    /// <summary>Adds <c>patternProperties</c>.</summary>
    public JsonSchemaBuilder PatternProperties(IEnumerable<(string Pattern, JsonSchema Schema)> properties) =>
        Set(new PatternPropertiesKeyword(ToPairs(properties)));

    /// <summary>Adds <c>patternProperties</c>.</summary>
    public JsonSchemaBuilder PatternProperties(IEnumerable<KeyValuePair<string, JsonSchema>> properties) =>
        Set(new PatternPropertiesKeyword(properties));

    /// <summary>Adds <c>additionalProperties</c>.</summary>
    public JsonSchemaBuilder AdditionalProperties(JsonSchema schema) => Set(AdditionalPropertiesKeyword.For(schema));

    /// <summary>Adds <c>propertyNames</c>.</summary>
    public JsonSchemaBuilder PropertyNames(JsonSchema schema) => Set(PropertyNamesKeyword.For(schema));

    //  Unevaluated vocabulary.

    /// <summary>Adds <c>unevaluatedItems</c>.</summary>
    public JsonSchemaBuilder UnevaluatedItems(JsonSchema schema) => Set(UnevaluatedItemsKeyword.For(schema));

    /// <summary>Adds <c>unevaluatedProperties</c>.</summary>
    public JsonSchemaBuilder UnevaluatedProperties(JsonSchema schema) => Set(UnevaluatedPropertiesKeyword.For(schema));

    //  Validation vocabulary.

    /// <summary>Adds <c>type</c> from one or more flags.</summary>
    /// <remarks>
    /// - One flag writes a string, for example <c>"string"</c>.
    /// - More than one flag writes an array in a fixed order: object, array, boolean, string, number, integer, null.
    /// - To write an array in your own order, or an array with one type, use <see cref="Type(ReadOnlySpan{JsonSchemaType})"/>.
    /// </remarks>
    public JsonSchemaBuilder Type(JsonSchemaType type) => Set(TypeKeyword.For(type));

    /// <summary>Adds <c>type</c> as an array, in the given order. Each value is a single type.</summary>
    public JsonSchemaBuilder Type(params ReadOnlySpan<JsonSchemaType> types) => Set(new TypeKeyword(ImmutableArray.Create(types)));

    /// <summary>Adds <c>type</c> as an array, in the given order. Each value is a single type.</summary>
    public JsonSchemaBuilder Type(IEnumerable<JsonSchemaType> types) => Set(new TypeKeyword(types));

    /// <summary>Adds <c>enum</c>.</summary>
    public JsonSchemaBuilder Enum(params ReadOnlySpan<JsonElement> values) => Set(new EnumKeyword(values.ToArray()));

    /// <summary>Adds <c>enum</c>.</summary>
    public JsonSchemaBuilder Enum(IEnumerable<JsonElement> values) => Set(new EnumKeyword(values));

    /// <summary>Adds <c>enum</c> with string values.</summary>
    public JsonSchemaBuilder Enum(params ReadOnlySpan<string> values) => Set(new EnumKeyword(ToStrings(values, nameof(values))));

    /// <summary>Adds <c>enum</c> with string values.</summary>
    public JsonSchemaBuilder Enum(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Enum(values is string[] array ? array : [.. values]);
    }

    /// <summary>Adds <c>const</c>.</summary>
    public JsonSchemaBuilder Const(JsonElement value) => Set(new ConstKeyword(value));

    /// <summary>Adds <c>const</c> with a string value, or <c>null</c> if <paramref name="value"/> is null.</summary>
    public JsonSchemaBuilder Const(string? value) => Set(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with an integer value.</summary>
    public JsonSchemaBuilder Const(long value) => Set(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with a decimal value.</summary>
    public JsonSchemaBuilder Const(decimal value) => Set(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with a floating-point value.</summary>
    public JsonSchemaBuilder Const(double value) => Set(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with a boolean value.</summary>
    public JsonSchemaBuilder Const(bool value) => Set(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with a GUID value, written as a string in the "D" format.</summary>
    public JsonSchemaBuilder Const(Guid value) => Set(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>multipleOf</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder MultipleOf(JsonNumber value) => Set(new MultipleOfKeyword(value));

    /// <summary>Adds <c>maximum</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder Maximum(JsonNumber value) => Set(new MaximumKeyword(value));

    /// <summary>Adds <c>exclusiveMaximum</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder ExclusiveMaximum(JsonNumber value) => Set(new ExclusiveMaximumKeyword(value));

    /// <summary>Adds <c>minimum</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder Minimum(JsonNumber value) => Set(new MinimumKeyword(value));

    /// <summary>Adds <c>exclusiveMinimum</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder ExclusiveMinimum(JsonNumber value) => Set(new ExclusiveMinimumKeyword(value));

    /// <summary>Adds <c>maxLength</c>.</summary>
    public JsonSchemaBuilder MaxLength(ulong value) => Set(MaxLengthKeyword.For(value));

    /// <summary>Adds <c>minLength</c>.</summary>
    public JsonSchemaBuilder MinLength(ulong value) => Set(MinLengthKeyword.For(value));

    /// <summary>Adds <c>pattern</c>. The value is an ECMA-262 regular expression.</summary>
    public JsonSchemaBuilder Pattern(string pattern) => Set(new PatternKeyword(pattern));

    /// <summary>Adds <c>maxItems</c>.</summary>
    public JsonSchemaBuilder MaxItems(ulong value) => Set(MaxItemsKeyword.For(value));

    /// <summary>Adds <c>minItems</c>.</summary>
    public JsonSchemaBuilder MinItems(ulong value) => Set(MinItemsKeyword.For(value));

    /// <summary>Adds <c>uniqueItems</c>.</summary>
    public JsonSchemaBuilder UniqueItems(bool value = true) => Set(UniqueItemsKeyword.For(value));

    /// <summary>Adds <c>maxContains</c>.</summary>
    public JsonSchemaBuilder MaxContains(ulong value) => Set(MaxContainsKeyword.For(value));

    /// <summary>Adds <c>minContains</c>.</summary>
    public JsonSchemaBuilder MinContains(ulong value) => Set(MinContainsKeyword.For(value));

    /// <summary>Adds <c>maxProperties</c>.</summary>
    public JsonSchemaBuilder MaxProperties(ulong value) => Set(MaxPropertiesKeyword.For(value));

    /// <summary>Adds <c>minProperties</c>.</summary>
    public JsonSchemaBuilder MinProperties(ulong value) => Set(MinPropertiesKeyword.For(value));

    /// <summary>Adds <c>required</c>.</summary>
    public JsonSchemaBuilder Required(params ReadOnlySpan<string> properties) =>
        Set(new RequiredKeyword(ImmutableArray.Create(properties)));

    /// <summary>Adds <c>required</c>.</summary>
    public JsonSchemaBuilder Required(IEnumerable<string> properties) => Set(new RequiredKeyword(properties));

    /// <summary>Adds <c>dependentRequired</c>.</summary>
    public JsonSchemaBuilder DependentRequired(params ReadOnlySpan<(string Property, IEnumerable<string> Required)> dependencies)
    {
        var pairs = new KeyValuePair<string, IEnumerable<string>>[dependencies.Length];
        for (var index = 0; index < pairs.Length; index++)
        {
            pairs[index] = KeyValuePair.Create(dependencies[index].Property, dependencies[index].Required);
        }

        return Set(new DependentRequiredKeyword(pairs));
    }

    /// <summary>Adds <c>dependentRequired</c>.</summary>
    public JsonSchemaBuilder DependentRequired(IEnumerable<(string Property, IEnumerable<string> Required)> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return Set(new DependentRequiredKeyword(dependencies.Select(dependency => KeyValuePair.Create(dependency.Property, dependency.Required))));
    }

    /// <summary>Adds <c>dependentRequired</c>.</summary>
    public JsonSchemaBuilder DependentRequired(IEnumerable<KeyValuePair<string, IEnumerable<string>>> dependencies) =>
        Set(new DependentRequiredKeyword(dependencies));

    //  Meta-data vocabulary.

    /// <summary>Adds <c>title</c>.</summary>
    public JsonSchemaBuilder Title(string title) => Set(new TitleKeyword(title));

    /// <summary>Adds <c>description</c>.</summary>
    public JsonSchemaBuilder Description(string description) => Set(new DescriptionKeyword(description));

    /// <summary>Adds <c>default</c>.</summary>
    public JsonSchemaBuilder Default(JsonElement value) => Set(new DefaultKeyword(value));

    /// <summary>Adds <c>default</c> with a string value, or <c>null</c> if <paramref name="value"/> is null.</summary>
    public JsonSchemaBuilder Default(string? value) => Set(new DefaultKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>default</c> with an integer value.</summary>
    public JsonSchemaBuilder Default(long value) => Set(new DefaultKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>default</c> with a decimal value.</summary>
    public JsonSchemaBuilder Default(decimal value) => Set(new DefaultKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>default</c> with a floating-point value.</summary>
    public JsonSchemaBuilder Default(double value) => Set(new DefaultKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>default</c> with a boolean value.</summary>
    public JsonSchemaBuilder Default(bool value) => Set(new DefaultKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>default</c> with a GUID value, written as a string in the "D" format.</summary>
    public JsonSchemaBuilder Default(Guid value) => Set(new DefaultKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>deprecated</c>.</summary>
    public JsonSchemaBuilder Deprecated(bool value = true) => Set(DeprecatedKeyword.For(value));

    /// <summary>Adds <c>readOnly</c>.</summary>
    public JsonSchemaBuilder ReadOnly(bool value = true) => Set(ReadOnlyKeyword.For(value));

    /// <summary>Adds <c>writeOnly</c>.</summary>
    public JsonSchemaBuilder WriteOnly(bool value = true) => Set(WriteOnlyKeyword.For(value));

    /// <summary>Adds <c>examples</c>.</summary>
    public JsonSchemaBuilder Examples(params ReadOnlySpan<JsonElement> values) => Set(new ExamplesKeyword(values.ToArray()));

    /// <summary>Adds <c>examples</c>.</summary>
    public JsonSchemaBuilder Examples(IEnumerable<JsonElement> values) => Set(new ExamplesKeyword(values));

    /// <summary>Adds <c>examples</c> with string values.</summary>
    public JsonSchemaBuilder Examples(params ReadOnlySpan<string> values) => Set(new ExamplesKeyword(ToStrings(values, nameof(values))));

    /// <summary>Adds <c>examples</c> with string values.</summary>
    public JsonSchemaBuilder Examples(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Examples(values is string[] array ? array : [.. values]);
    }

    //  Format-annotation and content vocabularies.

    /// <summary>Adds <c>format</c>. <see cref="JsonSchemaFormats"/> has the names that draft 2020-12 defines.</summary>
    public JsonSchemaBuilder Format(string format) => Set(FormatKeyword.For(format));

    /// <summary>Adds <c>contentEncoding</c>.</summary>
    public JsonSchemaBuilder ContentEncoding(string encoding) => Set(new ContentEncodingKeyword(encoding));

    /// <summary>Adds <c>contentMediaType</c>.</summary>
    public JsonSchemaBuilder ContentMediaType(string mediaType) => Set(new ContentMediaTypeKeyword(mediaType));

    /// <summary>Adds <c>contentSchema</c>.</summary>
    public JsonSchemaBuilder ContentSchema(JsonSchema schema) => Set(new ContentSchemaKeyword(schema));

    private int IndexOf(string name)
    {
        for (var position = 0; position < keywords.Count; position++)
        {
            if (string.Equals(keywords[position].Name, name, StringComparison.Ordinal))
            {
                return position;
            }
        }

        return -1;
    }

    private static ImmutableArray<string> ToStrings(ReadOnlySpan<string> values, string parameterName)
    {
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);
        }

        return ImmutableArray.Create(values);
    }

    private static JsonSchemaMap ToMap(ReadOnlySpan<(string Name, JsonSchema Schema)> pairs) => JsonSchemaMap.Create(pairs);

    private static IEnumerable<KeyValuePair<string, JsonSchema>> ToPairs(IEnumerable<(string Name, JsonSchema Schema)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        return pairs.Select(pair => KeyValuePair.Create(pair.Name, pair.Schema));
    }
}

/// <summary>
/// Creates standalone JSON values from .NET primitives without reflection.
/// </summary>
public static class JsonElements
{
    /// <summary>The JSON <c>null</c> value.</summary>
    public static JsonElement Null { get; } = JsonSerializer.SerializeToElement<string?>(null, PrimitiveJsonContext.Default.String);

    /// <summary>Creates a JSON string.</summary>
    public static JsonElement From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.SerializeToElement(value, PrimitiveJsonContext.Default.String);
    }

    /// <summary>Creates a JSON number.</summary>
    public static JsonElement From(long value) => JsonSerializer.SerializeToElement(value, PrimitiveJsonContext.Default.Int64);

    /// <summary>Creates a JSON number.</summary>
    public static JsonElement From(decimal value) =>
        JsonSerializer.SerializeToElement(value, PrimitiveJsonContext.Default.Decimal);

    /// <summary>Creates a JSON number.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is NaN or infinite. JSON cannot represent it.</exception>
    public static JsonElement From(double value) =>
        double.IsFinite(value)
            ? JsonSerializer.SerializeToElement(value, PrimitiveJsonContext.Default.Double)
            : throw new ArgumentException("JSON cannot represent NaN or infinity.", nameof(value));

    /// <summary>Creates a JSON boolean.</summary>
    public static JsonElement From(bool value) => JsonSerializer.SerializeToElement(value, PrimitiveJsonContext.Default.Boolean);

    /// <summary>Creates a JSON string in the GUID "D" format.</summary>
    public static JsonElement From(Guid value) => JsonSerializer.SerializeToElement(value, PrimitiveJsonContext.Default.Guid);
}

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Guid))]
internal sealed partial class PrimitiveJsonContext : JsonSerializerContext;
