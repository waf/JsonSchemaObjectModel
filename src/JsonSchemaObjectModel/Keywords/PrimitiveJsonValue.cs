using System.Buffers;
using System.Text.Json;

namespace JsonSchemaObjectModel.Keywords;

/// <summary>
/// A JSON primitive that a builder supplies as a .NET value.
/// </summary>
/// <remarks>
/// A <see cref="JsonElement"/> needs a document of its own.
/// This type writes the value directly, and creates the element only when a caller asks for it.
/// </remarks>
internal readonly struct PrimitiveJsonValue
{
    private readonly Kind kind;
    private readonly object? value;

    private PrimitiveJsonValue(Kind kind, object? value)
    {
        this.kind = kind;
        this.value = value;
    }

    private enum Kind
    {
        Null,
        True,
        False,
        String,
        Int64,
        Decimal,
        Double,
        Guid,
    }

    public static PrimitiveJsonValue Null => new(Kind.Null, null);

    public static PrimitiveJsonValue From(string? value) => value is null ? Null : new(Kind.String, value);

    public static PrimitiveJsonValue From(long value) => new(Kind.Int64, value);

    public static PrimitiveJsonValue From(decimal value) => new(Kind.Decimal, value);

    public static PrimitiveJsonValue From(double value) =>
        double.IsFinite(value) ? new(Kind.Double, value) : throw new ArgumentException("JSON cannot represent NaN or infinity.", nameof(value));

    public static PrimitiveJsonValue From(bool value) => new(value ? Kind.True : Kind.False, null);

    public static PrimitiveJsonValue From(Guid value) => new(Kind.Guid, value);

    public void WriteTo(Utf8JsonWriter writer)
    {
        switch (kind)
        {
            case Kind.Null:
                writer.WriteNullValue();
                break;
            case Kind.True:
                writer.WriteBooleanValue(true);
                break;
            case Kind.False:
                writer.WriteBooleanValue(false);
                break;
            case Kind.String:
                writer.WriteStringValue((string)value!);
                break;
            case Kind.Int64:
                writer.WriteNumberValue((long)value!);
                break;
            case Kind.Decimal:
                writer.WriteNumberValue((decimal)value!);
                break;
            case Kind.Double:
                writer.WriteNumberValue((double)value!);
                break;
            case Kind.Guid:
                writer.WriteStringValue((Guid)value!);
                break;
        }
    }

    public JsonElement ToElement()
    {
        var buffer = new ArrayBufferWriter<byte>(32);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteTo(writer);
        }

        return Parse(buffer.WrittenSpan);
    }

    /// <summary>
    /// Creates elements for many values in one document.
    /// </summary>
    public static JsonElement[] ToElements(ReadOnlySpan<PrimitiveJsonValue> values)
    {
        var buffer = new ArrayBufferWriter<byte>(16 * values.Length + 2);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                value.WriteTo(writer);
            }

            writer.WriteEndArray();
        }

        var array = Parse(buffer.WrittenSpan);
        var elements = new JsonElement[values.Length];
        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            elements[index++] = element;
        }

        return elements;
    }

    private static JsonElement Parse(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json);
        return JsonElement.ParseValue(ref reader);
    }
}

/// <summary>
/// Holds a lazily created element, so that a race between threads creates at most a spare copy.
/// </summary>
internal sealed class ElementBox<T>(T value)
{
    public T Value { get; } = value;
}
