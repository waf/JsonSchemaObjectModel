using System.Buffers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace JsonSchemaObjectModel.Keywords;

/// <summary>
/// A JSON primitive that a builder supplies as a .NET value, for the <c>const</c> keyword.
/// </summary>
/// <remarks>
/// - A <see cref="JsonElement"/> needs a document of its own.
///   This type writes the value directly, and creates the element only when a caller asks for it.
/// - The numeric and GUID values share one 16-byte field, so no value is boxed.
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
internal readonly struct PrimitiveJsonValue
{
    [FieldOffset(0)]
    private readonly string? text;

    [FieldOffset(8)]
    private readonly Kind kind;

    [FieldOffset(16)]
    private readonly long int64;

    [FieldOffset(16)]
    private readonly double number;

    [FieldOffset(16)]
    private readonly decimal decimalNumber;

    [FieldOffset(16)]
    private readonly Guid guid;

    private PrimitiveJsonValue(Kind kind) => this.kind = kind;

    private PrimitiveJsonValue(string text)
    {
        kind = Kind.String;
        this.text = text;
    }

    private PrimitiveJsonValue(long value)
    {
        kind = Kind.Int64;
        int64 = value;
    }

    private PrimitiveJsonValue(double value)
    {
        kind = Kind.Double;
        number = value;
    }

    private PrimitiveJsonValue(decimal value)
    {
        kind = Kind.Decimal;
        decimalNumber = value;
    }

    private PrimitiveJsonValue(Guid value)
    {
        kind = Kind.Guid;
        guid = value;
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

    public static PrimitiveJsonValue Null => new(Kind.Null);

    public static PrimitiveJsonValue From(string? value) => value is null ? Null : new(value);

    public static PrimitiveJsonValue From(long value) => new(value);

    public static PrimitiveJsonValue From(decimal value) => new(value);

    public static PrimitiveJsonValue From(double value) =>
        double.IsFinite(value) ? new(value) : throw new ArgumentException("JSON cannot represent NaN or infinity.", nameof(value));

    public static PrimitiveJsonValue From(bool value) => new(value ? Kind.True : Kind.False);

    public static PrimitiveJsonValue From(Guid value) => new(value);

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
                writer.WriteStringValue(text);
                break;
            case Kind.Int64:
                writer.WriteNumberValue(int64);
                break;
            case Kind.Decimal:
                writer.WriteNumberValue(decimalNumber);
                break;
            case Kind.Double:
                writer.WriteNumberValue(number);
                break;
            case Kind.Guid:
                writer.WriteStringValue(guid);
                break;
        }
    }

    /// <summary>
    /// Returns the hash code of the JSON that <see cref="WriteTo"/> writes. See <see cref="JsonValueEquality"/>.
    /// </summary>
    public int GetValueHashCode()
    {
        switch (kind)
        {
            case Kind.Null:
                return JsonValueEquality.Null;
            case Kind.True:
                return JsonValueEquality.Boolean(true);
            case Kind.False:
                return JsonValueEquality.Boolean(false);
            case Kind.String:
                return JsonValueEquality.String(text!);
            case Kind.Int64:
                return JsonValueEquality.Number(JsonNumber.From(int64));
            case Kind.Decimal:
                return JsonValueEquality.Number(JsonNumber.From(decimalNumber));
            case Kind.Double:
                return JsonValueEquality.Number(JsonNumber.From(number));
            default:
                Span<char> chars = stackalloc char[36];
                guid.TryFormat(chars, out var length);
                return JsonValueEquality.String(chars[..length]);
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
    /// Creates elements for many strings in one document.
    /// </summary>
    public static JsonElement[] ToElements(ReadOnlySpan<string> values)
    {
        var buffer = new ArrayBufferWriter<byte>(16 * values.Length + 2);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
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
