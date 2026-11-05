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
    private readonly OrderedDictionary<string, JsonSchema> schemas;

    /// <summary>
    /// Creates the keyword. The order of <paramref name="schemas"/> is kept.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="schemas"/> contains the same name more than once.</exception>
    protected SubschemaMapKeyword(IEnumerable<KeyValuePair<string, JsonSchema>> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        if (schemas is OwnedSchemaMap owned)
        {
            //  Only this library creates an owned map, and it does not keep a reference to it.
            this.schemas = owned;
        }
        else
        {
            var ordered = new OwnedSchemaMap(schemas.TryGetNonEnumeratedCount(out var count) ? count : 0);
            foreach (var (name, schema) in schemas)
            {
                ordered.AddUnique(name, schema, nameof(schemas));
            }

            this.schemas = ordered;
        }

        Schemas = new ReadOnlyDictionary<string, JsonSchema>(this.schemas);
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, JsonSchema> Schemas { get; }

    /// <summary>
    /// Gets a subschema by name. For a small map, this does not allocate a string.
    /// </summary>
    public bool TryGetSchema(ReadOnlySpan<char> name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsonSchema? schema)
    {
        //  OrderedDictionary has no span lookup. Maps in a schema are small, and a linear scan of a few names is fast.
        const int MaxLinearScan = 16;
        if (schemas.Count > MaxLinearScan)
        {
            return schemas.TryGetValue(name.ToString(), out schema);
        }

        for (var index = 0; index < schemas.Count; index++)
        {
            var (key, value) = schemas.GetAt(index);
            if (name.SequenceEqual(key))
            {
                schema = value;
                return true;
            }
        }

        schema = null;
        return false;
    }

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (name, schema) in schemas)
        {
            writer.WritePropertyName(name);
            schema.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// An ordered map that only this library creates. A keyword can keep it without a copy.
/// </summary>
internal sealed class OwnedSchemaMap(int capacity) : OrderedDictionary<string, JsonSchema>(capacity, StringComparer.Ordinal)
{
    public void AddUnique(string name, JsonSchema schema, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(name, parameterName);
        ArgumentNullException.ThrowIfNull(schema, parameterName);
        if (!TryAdd(name, schema))
        {
            throw new ArgumentException($"The name '{name}' occurs more than once.", parameterName);
        }
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
/// The keyword keeps the number as JSON, so a value that does not fit <see cref="decimal"/> or <see cref="double"/>
/// still round-trips.
/// </remarks>
public abstract class NumberValueKeyword : IJsonSchemaKeyword
{
    /// <summary>
    /// Creates the keyword.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a JSON number.</exception>
    protected NumberValueKeyword(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new ArgumentException("The value must be a JSON number.", nameof(value));
        }

        Value = value.Clone();
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The keyword value, as JSON.
    /// </summary>
    public JsonElement Value { get; }

    /// <summary>
    /// Gets the value as a <see cref="decimal"/>. This fails if the value is out of range.
    /// </summary>
    public bool TryGetDecimal(out decimal value) => Value.TryGetDecimal(out value);

    /// <summary>
    /// Gets the value as a <see cref="double"/>. This can lose precision.
    /// </summary>
    public double GetDouble() => Value.GetDouble();

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
    private readonly ImmutableArray<PrimitiveJsonValue> primitives;
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

    private protected JsonValueArrayKeyword(ImmutableArray<PrimitiveJsonValue> values) => primitives = values;

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// The values, in document order.
    /// </summary>
    public ImmutableArray<JsonElement> Values =>
        parsed.IsDefault
            ? (materialized ??= new(ImmutableCollectionsMarshal.AsImmutableArray(PrimitiveJsonValue.ToElements(primitives.AsSpan())))).Value
            : parsed;

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        if (parsed.IsDefault)
        {
            foreach (var value in primitives)
            {
                value.WriteTo(writer);
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
