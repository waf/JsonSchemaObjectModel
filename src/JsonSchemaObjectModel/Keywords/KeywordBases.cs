using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace JsonSchemaObjectModel.Keywords;

/// <summary>
/// A keyword whose value is one subschema.
/// </summary>
public abstract class SingleSubschemaKeyword(JsonSchema schema) : ISingleSubschemaKeyword
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public JsonSchema Schema { get; } = schema ?? throw new ArgumentNullException(nameof(schema));

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer) => Schema.WriteTo(writer);
}

/// <summary>
/// A keyword whose value is a non-empty array of subschemas.
/// </summary>
public abstract class SubschemaArrayKeyword : ISubschemaArrayKeyword
{
    /// <summary>
    /// Creates the keyword. An <see cref="ImmutableArray{T}"/> is used without a copy.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="schemas"/> is empty.</exception>
    protected SubschemaArrayKeyword(IEnumerable<JsonSchema> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        Schemas = schemas is ImmutableArray<JsonSchema> immutable ? immutable : [.. schemas];
        if (Schemas.IsEmpty)
        {
            throw new ArgumentException("The array must contain at least one schema.", nameof(schemas));
        }

        foreach (var schema in Schemas)
        {
            ArgumentNullException.ThrowIfNull(schema, nameof(schemas));
        }
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public ImmutableArray<JsonSchema> Schemas { get; }

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var schema in Schemas)
        {
            schema.WriteTo(writer);
        }

        writer.WriteEndArray();
    }
}

/// <summary>
/// A keyword whose value is an object of named subschemas.
/// </summary>
public abstract class SubschemaMapKeyword : ISubschemaMapKeyword
{
    /// <summary>
    /// Creates the keyword. The order of <paramref name="schemas"/> is kept. A <see cref="SchemaMap"/> is used without a copy.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="schemas"/> contains the same name more than once.</exception>
    protected SubschemaMapKeyword(IEnumerable<KeyValuePair<string, JsonSchema>> schemas) => Schemas = SchemaMap.Create(schemas);

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public SchemaMap Schemas { get; }

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (name, schema) in Schemas.Entries)
        {
            writer.WritePropertyName(name);
            schema.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// A keyword whose value is a string.
/// </summary>
public abstract class StringValueKeyword(string value) : IJsonSchemaKeyword
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The keyword value.
    /// </summary>
    public string Value { get; } = value ?? throw new ArgumentNullException(nameof(value));

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer) => writer.WriteStringValue(Value);
}

/// <summary>
/// A keyword whose value is a boolean.
/// </summary>
public abstract class BooleanValueKeyword(bool value) : IJsonSchemaKeyword
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The keyword value.
    /// </summary>
    public bool Value { get; } = value;

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer) => writer.WriteBooleanValue(Value);
}

/// <summary>
/// A keyword whose value is a JSON number.
/// </summary>
/// <remarks>
/// The keyword keeps the number as a <see cref="JsonNumber"/>, so a value that does not fit <see cref="decimal"/>
/// or <see cref="double"/> still round-trips.
/// </remarks>
public abstract class NumberValueKeyword(JsonNumber value) : IJsonSchemaKeyword
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The keyword value.
    /// </summary>
    public JsonNumber Value { get; } = value;

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer) => Value.WriteTo(writer);
}

/// <summary>
/// A keyword whose value is a non-negative integer.
/// </summary>
public abstract class NonNegativeIntegerKeyword(ulong value) : IJsonSchemaKeyword
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The keyword value.
    /// </summary>
    public ulong Value { get; } = value;

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer) => writer.WriteNumberValue(Value);
}

/// <summary>
/// A keyword whose value is any JSON value.
/// </summary>
public abstract class JsonValueKeyword : IJsonSchemaKeyword
{
    private readonly JsonElement parsed;
    private readonly PrimitiveJsonValue primitive;
    private readonly bool isPrimitive;
    private ElementBox<JsonElement>? materialized;

    /// <summary>
    /// Creates the keyword.
    /// </summary>
    protected JsonValueKeyword(JsonElement value) => parsed = value.Clone();

    private protected JsonValueKeyword(PrimitiveJsonValue value)
    {
        primitive = value;
        isPrimitive = true;
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The keyword value.
    /// </summary>
    public JsonElement Value => isPrimitive ? (materialized ??= new(primitive.ToElement())).Value : parsed;

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        if (isPrimitive)
        {
            primitive.WriteTo(writer);
        }
        else
        {
            parsed.WriteTo(writer);
        }
    }
}

/// <summary>
/// A keyword whose value is an array of JSON values.
/// </summary>
public abstract class JsonValueArrayKeyword : IJsonSchemaKeyword
{
    private readonly ImmutableArray<JsonElement> parsed;

    //  A builder's string values. They are written directly, and become elements only when Values is read.
    private readonly ImmutableArray<string> strings;
    private ElementBox<ImmutableArray<JsonElement>>? materialized;

    /// <summary>
    /// Creates the keyword.
    /// </summary>
    protected JsonValueArrayKeyword(IEnumerable<JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        parsed = [.. values.Select(value => value.Clone())];
    }

    private protected JsonValueArrayKeyword(ImmutableArray<JsonElement> clonedValues) => parsed = clonedValues;

    private protected JsonValueArrayKeyword(ImmutableArray<string> strings) => this.strings = strings;

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The values, in document order.
    /// </summary>
    public ImmutableArray<JsonElement> Values =>
        parsed.IsDefault
            ? (materialized ??= new(ImmutableCollectionsMarshal.AsImmutableArray(PrimitiveJsonValue.ToElements(strings.AsSpan())))).Value
            : parsed;

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        if (parsed.IsDefault)
        {
            foreach (var value in strings)
            {
                writer.WriteStringValue(value);
            }
        }
        else
        {
            foreach (var value in parsed)
            {
                value.WriteTo(writer);
            }
        }

        writer.WriteEndArray();
    }
}
