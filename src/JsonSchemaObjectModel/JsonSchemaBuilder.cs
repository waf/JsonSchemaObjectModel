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
/// - Each method adds a keyword. A keyword with the same name replaces the earlier one and keeps its position.
/// - The builder converts to <see cref="JsonSchema"/> implicitly, so you can pass it where a schema is expected.
/// - Add a custom keyword with <see cref="Add"/>.
/// </remarks>
public sealed class JsonSchemaBuilder
{
    private readonly List<IJsonSchemaKeyword> keywords = [];

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
    /// Adds a keyword, or replaces the keyword that has the same name.
    /// </summary>
    /// <remarks>
    /// The typed accessors, for example <c>schema.Type</c>, only see typed keywords.
    /// An <see cref="UnknownKeyword"/> with a standard name is kept and written, but those accessors do not see it.
    /// </remarks>
    public JsonSchemaBuilder Add(IJsonSchemaKeyword keyword)
    {
        ArgumentNullException.ThrowIfNull(keyword);
        for (var position = 0; position < keywords.Count; position++)
        {
            if (string.Equals(keywords[position].Name, keyword.Name, StringComparison.Ordinal))
            {
                keywords[position] = keyword;
                return this;
            }
        }

        keywords.Add(keyword);
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

    /// <summary>Adds <c>$schema</c>.</summary>
    public JsonSchemaBuilder Schema(string uri) => Add(new SchemaKeyword(uri));

    /// <summary>Adds <c>$id</c>.</summary>
    public JsonSchemaBuilder Id(string uri) => Add(new IdKeyword(uri));

    /// <summary>Adds <c>$ref</c>.</summary>
    public JsonSchemaBuilder Ref(string uri) => Add(new RefKeyword(uri));

    /// <summary>Adds <c>$anchor</c>.</summary>
    public JsonSchemaBuilder Anchor(string name) => Add(new AnchorKeyword(name));

    /// <summary>Adds <c>$dynamicRef</c>.</summary>
    public JsonSchemaBuilder DynamicRef(string uri) => Add(new DynamicRefKeyword(uri));

    /// <summary>Adds <c>$dynamicAnchor</c>.</summary>
    public JsonSchemaBuilder DynamicAnchor(string name) => Add(new DynamicAnchorKeyword(name));

    /// <summary>Adds <c>$comment</c>.</summary>
    public JsonSchemaBuilder Comment(string comment) => Add(new CommentKeyword(comment));

    /// <summary>Adds <c>$vocabulary</c>.</summary>
    public JsonSchemaBuilder Vocabulary(params IEnumerable<(string Uri, bool Required)> vocabularies) =>
        Add(new VocabularyKeyword(vocabularies.Select(vocabulary => KeyValuePair.Create(vocabulary.Uri, vocabulary.Required))));

    /// <summary>Adds <c>$defs</c>.</summary>
    public JsonSchemaBuilder Defs(params ReadOnlySpan<(string Name, JsonSchema Schema)> definitions) =>
        Add(new DefsKeyword(ToMap(definitions)));

    /// <summary>Adds <c>$defs</c>.</summary>
    public JsonSchemaBuilder Defs(IEnumerable<(string Name, JsonSchema Schema)> definitions) =>
        Add(new DefsKeyword(ToPairs(definitions)));

    /// <summary>Adds <c>$defs</c>.</summary>
    public JsonSchemaBuilder Defs(IEnumerable<KeyValuePair<string, JsonSchema>> definitions) =>
        Add(new DefsKeyword(definitions));

    //  Applicator vocabulary.

    /// <summary>Adds <c>allOf</c>.</summary>
    public JsonSchemaBuilder AllOf(params ReadOnlySpan<JsonSchema> schemas) => Add(new AllOfKeyword(ImmutableArray.Create(schemas)));

    /// <summary>Adds <c>allOf</c>.</summary>
    public JsonSchemaBuilder AllOf(IEnumerable<JsonSchema> schemas) => Add(new AllOfKeyword(schemas));

    /// <summary>Adds <c>anyOf</c>.</summary>
    public JsonSchemaBuilder AnyOf(params ReadOnlySpan<JsonSchema> schemas) => Add(new AnyOfKeyword(ImmutableArray.Create(schemas)));

    /// <summary>Adds <c>anyOf</c>.</summary>
    public JsonSchemaBuilder AnyOf(IEnumerable<JsonSchema> schemas) => Add(new AnyOfKeyword(schemas));

    /// <summary>Adds <c>oneOf</c>.</summary>
    public JsonSchemaBuilder OneOf(params ReadOnlySpan<JsonSchema> schemas) => Add(new OneOfKeyword(ImmutableArray.Create(schemas)));

    /// <summary>Adds <c>oneOf</c>.</summary>
    public JsonSchemaBuilder OneOf(IEnumerable<JsonSchema> schemas) => Add(new OneOfKeyword(schemas));

    /// <summary>Adds <c>not</c>.</summary>
    public JsonSchemaBuilder Not(JsonSchema schema) => Add(NotKeyword.For(schema));

    /// <summary>Adds <c>if</c>.</summary>
    public JsonSchemaBuilder If(JsonSchema schema) => Add(new IfKeyword(schema));

    /// <summary>Adds <c>then</c>.</summary>
    public JsonSchemaBuilder Then(JsonSchema schema) => Add(new ThenKeyword(schema));

    /// <summary>Adds <c>else</c>.</summary>
    public JsonSchemaBuilder Else(JsonSchema schema) => Add(new ElseKeyword(schema));

    /// <summary>Adds <c>dependentSchemas</c>.</summary>
    public JsonSchemaBuilder DependentSchemas(params ReadOnlySpan<(string Name, JsonSchema Schema)> schemas) =>
        Add(new DependentSchemasKeyword(ToMap(schemas)));

    /// <summary>Adds <c>dependentSchemas</c>.</summary>
    public JsonSchemaBuilder DependentSchemas(IEnumerable<(string Name, JsonSchema Schema)> schemas) =>
        Add(new DependentSchemasKeyword(ToPairs(schemas)));

    /// <summary>Adds <c>prefixItems</c>.</summary>
    public JsonSchemaBuilder PrefixItems(params ReadOnlySpan<JsonSchema> schemas) => Add(new PrefixItemsKeyword(ImmutableArray.Create(schemas)));

    /// <summary>Adds <c>prefixItems</c>.</summary>
    public JsonSchemaBuilder PrefixItems(IEnumerable<JsonSchema> schemas) => Add(new PrefixItemsKeyword(schemas));

    /// <summary>Adds <c>items</c>.</summary>
    public JsonSchemaBuilder Items(JsonSchema schema) => Add(ItemsKeyword.For(schema));

    /// <summary>Adds <c>contains</c>.</summary>
    public JsonSchemaBuilder Contains(JsonSchema schema) => Add(ContainsKeyword.For(schema));

    /// <summary>Adds <c>properties</c>.</summary>
    public JsonSchemaBuilder Properties(params ReadOnlySpan<(string Name, JsonSchema Schema)> properties) =>
        Add(new PropertiesKeyword(ToMap(properties)));

    /// <summary>Adds <c>properties</c>.</summary>
    public JsonSchemaBuilder Properties(IEnumerable<(string Name, JsonSchema Schema)> properties) =>
        Add(new PropertiesKeyword(ToPairs(properties)));

    /// <summary>Adds <c>properties</c>.</summary>
    public JsonSchemaBuilder Properties(IEnumerable<KeyValuePair<string, JsonSchema>> properties) =>
        Add(new PropertiesKeyword(properties));

    /// <summary>Adds <c>patternProperties</c>.</summary>
    public JsonSchemaBuilder PatternProperties(params ReadOnlySpan<(string Pattern, JsonSchema Schema)> properties) =>
        Add(new PatternPropertiesKeyword(ToMap(properties)));

    /// <summary>Adds <c>patternProperties</c>.</summary>
    public JsonSchemaBuilder PatternProperties(IEnumerable<(string Pattern, JsonSchema Schema)> properties) =>
        Add(new PatternPropertiesKeyword(ToPairs(properties)));

    /// <summary>Adds <c>additionalProperties</c>.</summary>
    public JsonSchemaBuilder AdditionalProperties(JsonSchema schema) => Add(AdditionalPropertiesKeyword.For(schema));

    /// <summary>Adds <c>propertyNames</c>.</summary>
    public JsonSchemaBuilder PropertyNames(JsonSchema schema) => Add(PropertyNamesKeyword.For(schema));

    //  Unevaluated vocabulary.

    /// <summary>Adds <c>unevaluatedItems</c>.</summary>
    public JsonSchemaBuilder UnevaluatedItems(JsonSchema schema) => Add(UnevaluatedItemsKeyword.For(schema));

    /// <summary>Adds <c>unevaluatedProperties</c>.</summary>
    public JsonSchemaBuilder UnevaluatedProperties(JsonSchema schema) => Add(UnevaluatedPropertiesKeyword.For(schema));

    //  Validation vocabulary.

    /// <summary>Adds <c>type</c>. One flag writes a string. More than one flag writes an array.</summary>
    public JsonSchemaBuilder Type(SchemaValueType type) => Add(TypeKeyword.For(type));

    /// <summary>Adds <c>enum</c>.</summary>
    public JsonSchemaBuilder Enum(params ReadOnlySpan<JsonElement> values) => Add(new EnumKeyword(values.ToArray()));

    /// <summary>Adds <c>enum</c>.</summary>
    public JsonSchemaBuilder Enum(IEnumerable<JsonElement> values) => Add(new EnumKeyword(values));

    /// <summary>Adds <c>enum</c> with string values.</summary>
    public JsonSchemaBuilder Enum(params ReadOnlySpan<string> values)
    {
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(values));
        }

        return Add(new EnumKeyword(ImmutableArray.Create(values)));
    }

    /// <summary>Adds <c>enum</c> with string values.</summary>
    public JsonSchemaBuilder Enum(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Enum(values is string[] array ? array : [.. values]);
    }

    /// <summary>Adds <c>const</c>.</summary>
    public JsonSchemaBuilder Const(JsonElement value) => Add(new ConstKeyword(value));

    /// <summary>Adds <c>const</c> with a string value, or <c>null</c> if <paramref name="value"/> is null.</summary>
    public JsonSchemaBuilder Const(string? value) => Add(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with an integer value.</summary>
    public JsonSchemaBuilder Const(long value) => Add(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with a decimal value.</summary>
    public JsonSchemaBuilder Const(decimal value) => Add(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with a floating-point value.</summary>
    public JsonSchemaBuilder Const(double value) => Add(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with a boolean value.</summary>
    public JsonSchemaBuilder Const(bool value) => Add(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>const</c> with a GUID value, written as a string in the "D" format.</summary>
    public JsonSchemaBuilder Const(Guid value) => Add(new ConstKeyword(PrimitiveJsonValue.From(value)));

    /// <summary>Adds <c>multipleOf</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder MultipleOf(JsonNumber value) => Add(new MultipleOfKeyword(value));

    /// <summary>Adds <c>maximum</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder Maximum(JsonNumber value) => Add(new MaximumKeyword(value));

    /// <summary>Adds <c>exclusiveMaximum</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder ExclusiveMaximum(JsonNumber value) => Add(new ExclusiveMaximumKeyword(value));

    /// <summary>Adds <c>minimum</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder Minimum(JsonNumber value) => Add(new MinimumKeyword(value));

    /// <summary>Adds <c>exclusiveMinimum</c>.</summary>
    /// <remarks>An <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/> converts implicitly. For a <see cref="double"/>, use <see cref="JsonNumber.From(double)"/>.</remarks>
    public JsonSchemaBuilder ExclusiveMinimum(JsonNumber value) => Add(new ExclusiveMinimumKeyword(value));

    /// <summary>Adds <c>maxLength</c>.</summary>
    public JsonSchemaBuilder MaxLength(ulong value) => Add(MaxLengthKeyword.For(value));

    /// <summary>Adds <c>minLength</c>.</summary>
    public JsonSchemaBuilder MinLength(ulong value) => Add(MinLengthKeyword.For(value));

    /// <summary>Adds <c>pattern</c>. The value is an ECMA-262 regular expression.</summary>
    public JsonSchemaBuilder Pattern(string pattern) => Add(new PatternKeyword(pattern));

    /// <summary>Adds <c>maxItems</c>.</summary>
    public JsonSchemaBuilder MaxItems(ulong value) => Add(MaxItemsKeyword.For(value));

    /// <summary>Adds <c>minItems</c>.</summary>
    public JsonSchemaBuilder MinItems(ulong value) => Add(MinItemsKeyword.For(value));

    /// <summary>Adds <c>uniqueItems</c>.</summary>
    public JsonSchemaBuilder UniqueItems(bool value = true) => Add(UniqueItemsKeyword.For(value));

    /// <summary>Adds <c>maxContains</c>.</summary>
    public JsonSchemaBuilder MaxContains(ulong value) => Add(MaxContainsKeyword.For(value));

    /// <summary>Adds <c>minContains</c>.</summary>
    public JsonSchemaBuilder MinContains(ulong value) => Add(MinContainsKeyword.For(value));

    /// <summary>Adds <c>maxProperties</c>.</summary>
    public JsonSchemaBuilder MaxProperties(ulong value) => Add(MaxPropertiesKeyword.For(value));

    /// <summary>Adds <c>minProperties</c>.</summary>
    public JsonSchemaBuilder MinProperties(ulong value) => Add(MinPropertiesKeyword.For(value));

    /// <summary>Adds <c>required</c>.</summary>
    public JsonSchemaBuilder Required(params ReadOnlySpan<string> properties) =>
        Add(new RequiredKeyword(ImmutableArray.Create(properties)));

    /// <summary>Adds <c>required</c>.</summary>
    public JsonSchemaBuilder Required(IEnumerable<string> properties) => Add(new RequiredKeyword(properties));

    /// <summary>Adds <c>dependentRequired</c>.</summary>
    public JsonSchemaBuilder DependentRequired(params IEnumerable<(string Property, IEnumerable<string> Required)> dependencies) =>
        Add(new DependentRequiredKeyword(dependencies.Select(dependency => KeyValuePair.Create(dependency.Property, dependency.Required))));

    //  Meta-data vocabulary.

    /// <summary>Adds <c>title</c>.</summary>
    public JsonSchemaBuilder Title(string title) => Add(new TitleKeyword(title));

    /// <summary>Adds <c>description</c>.</summary>
    public JsonSchemaBuilder Description(string description) => Add(new DescriptionKeyword(description));

    /// <summary>Adds <c>default</c>.</summary>
    public JsonSchemaBuilder Default(JsonElement value) => Add(new DefaultKeyword(value));

    /// <summary>Adds <c>deprecated</c>.</summary>
    public JsonSchemaBuilder Deprecated(bool value = true) => Add(DeprecatedKeyword.For(value));

    /// <summary>Adds <c>readOnly</c>.</summary>
    public JsonSchemaBuilder ReadOnly(bool value = true) => Add(ReadOnlyKeyword.For(value));

    /// <summary>Adds <c>writeOnly</c>.</summary>
    public JsonSchemaBuilder WriteOnly(bool value = true) => Add(WriteOnlyKeyword.For(value));

    /// <summary>Adds <c>examples</c>.</summary>
    public JsonSchemaBuilder Examples(params IEnumerable<JsonElement> values) => Add(new ExamplesKeyword(values));

    //  Format-annotation and content vocabularies.

    /// <summary>Adds <c>format</c>. <see cref="Formats"/> has the names that draft 2020-12 defines.</summary>
    public JsonSchemaBuilder Format(string format) => Add(FormatKeyword.For(format));

    /// <summary>Adds <c>contentEncoding</c>.</summary>
    public JsonSchemaBuilder ContentEncoding(string encoding) => Add(new ContentEncodingKeyword(encoding));

    /// <summary>Adds <c>contentMediaType</c>.</summary>
    public JsonSchemaBuilder ContentMediaType(string mediaType) => Add(new ContentMediaTypeKeyword(mediaType));

    /// <summary>Adds <c>contentSchema</c>.</summary>
    public JsonSchemaBuilder ContentSchema(JsonSchema schema) => Add(new ContentSchemaKeyword(schema));

    private static SchemaMap ToMap(ReadOnlySpan<(string Name, JsonSchema Schema)> pairs) => SchemaMap.Create(pairs);

    private static IEnumerable<KeyValuePair<string, JsonSchema>> ToPairs(IEnumerable<(string Name, JsonSchema Schema)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        return pairs.Select(pair => KeyValuePair.Create(pair.Name, pair.Schema));
    }
}

/// <summary>
/// Creates standalone JSON values from .NET primitives without reflection.
/// </summary>
public static class JsonValues
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
