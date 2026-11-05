using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace JsonSchemaObjectModel;

/// <summary>
/// A JSON number of any size or precision.
/// </summary>
/// <remarks>
/// - A parsed number keeps its JSON text, so a value beyond <see cref="decimal"/> or <see cref="double"/> survives,
///   for example <c>1e400</c>.
/// - A number from .NET (<see cref="From(long)"/>, <see cref="From(decimal)"/>, <see cref="From(double)"/>)
///   is kept as that value. It needs no JSON document.
/// - Equality compares values, not text: <c>1</c>, <c>1.0</c> and <c>10e-1</c> are equal.
///   This agrees with <see cref="JsonElement.DeepEquals"/>.
/// - The default value is the number zero.
/// </remarks>
public readonly struct JsonNumber : IEquatable<JsonNumber>
{
    //  Enough for the text of any long, decimal or double.
    private const int MaxFormattedLength = 64;

    private readonly JsonElement element;
    private readonly NumericValue value;
    private readonly Kind kind;

    private JsonNumber(JsonElement element)
    {
        kind = Kind.Element;
        this.element = element;
    }

    private JsonNumber(long value)
    {
        kind = Kind.Int64;
        this.value = new() { Int64 = value };
    }

    private JsonNumber(double value)
    {
        kind = Kind.Double;
        this.value = new() { Double = value };
    }

    private JsonNumber(decimal value)
    {
        kind = Kind.Decimal;
        this.value = new() { Decimal = value };
    }

    //  Int64 is 0, so the default value is the number zero.
    private enum Kind
    {
        Int64,
        Decimal,
        Double,
        Element,
    }

    /// <summary>
    /// The sign of the number: -1, 0 or 1. Every zero, also <c>-0</c>, has the sign 0.
    /// </summary>
    public int Sign =>
        kind switch
        {
            Kind.Int64 => Math.Sign(value.Int64),
            Kind.Decimal => Math.Sign(value.Decimal),
            Kind.Double => Math.Sign(value.Double),
            _ => JsonNumberParts.From(element).Sign,
        };

    /// <summary>
    /// <see langword="true"/> if the number has no fractional part, for example <c>2</c>, <c>2.0</c> or <c>1.5e1</c>.
    /// </summary>
    public bool IsInteger =>
        kind switch
        {
            Kind.Int64 => true,
            Kind.Decimal => decimal.IsInteger(value.Decimal),
            Kind.Double => double.IsInteger(value.Double),
            _ => JsonNumberParts.From(element).IsInteger,
        };

    /// <summary>
    /// Converts an integer to a number.
    /// </summary>
    public static implicit operator JsonNumber(long value) => new(value);

    /// <summary>
    /// Converts a decimal to a number. The scale is kept, so <c>1.50m</c> is written as <c>1.50</c>.
    /// </summary>
    public static implicit operator JsonNumber(decimal value) => new(value);

    /// <summary>
    /// Returns <see langword="true"/> if two numbers have the same value.
    /// </summary>
    public static bool operator ==(JsonNumber left, JsonNumber right) => left.Equals(right);

    /// <summary>
    /// Returns <see langword="true"/> if two numbers have different values.
    /// </summary>
    public static bool operator !=(JsonNumber left, JsonNumber right) => !left.Equals(right);

    /// <summary>
    /// Creates a number from an integer.
    /// </summary>
    public static JsonNumber From(long value) => new(value);

    /// <summary>
    /// Creates a number from a decimal. The scale is kept, so <c>1.50m</c> is written as <c>1.50</c>.
    /// </summary>
    public static JsonNumber From(decimal value) => new(value);

    /// <summary>
    /// Creates a number from a double.
    /// </summary>
    /// <remarks>
    /// There is no implicit conversion from <see cref="double"/>, because NaN and infinity are not JSON numbers.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="value"/> is NaN or infinite.</exception>
    public static JsonNumber From(double value) =>
        double.IsFinite(value) ? new(value) : throw new ArgumentException("JSON cannot represent NaN or infinity.", nameof(value));

    /// <summary>
    /// Creates a number from a JSON number element. The number does not depend on the element's document
    /// if that document is disposable.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="element"/> is not a JSON number.</exception>
    public static JsonNumber FromElement(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number
            ? new(element.Clone())
            : throw new ArgumentException("The element must be a JSON number.", nameof(element));

    /// <summary>
    /// Parses JSON number text, for example <c>-1.5e10</c>.
    /// </summary>
    /// <exception cref="FormatException"><paramref name="text"/> is not a JSON number.</exception>
    public static JsonNumber Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var parsed) ? parsed : throw new FormatException($"'{text}' is not a JSON number.");
    }

    /// <summary>
    /// Parses JSON number text, for example <c>-1.5e10</c>. The text must have no white space.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out JsonNumber number)
    {
        number = default;
        if (text.IsEmpty || char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]))
        {
            return false;
        }

        var maxLength = Encoding.UTF8.GetMaxByteCount(text.Length);
        var rented = maxLength > MaxFormattedLength ? ArrayPool<byte>.Shared.Rent(maxLength) : null;
        try
        {
            var buffer = rented ?? stackalloc byte[MaxFormattedLength];
            var utf8 = buffer[..Encoding.UTF8.GetBytes(text, buffer)];
            if (!IsSingleNumber(utf8))
            {
                return false;
            }

            var reader = new Utf8JsonReader(utf8);
            number = new JsonNumber(JsonElement.ParseValue(ref reader));
            return true;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Gets the value as a <see cref="long"/>. This fails if the number is not an integer, or is out of range.
    /// </summary>
    /// <remarks>A number with a zero fractional part, for example <c>2.0</c>, is an integer.</remarks>
    public bool TryGetInt64(out long value)
    {
        if (kind == Kind.Int64)
        {
            value = this.value.Int64;
            return true;
        }

        Span<byte> scratch = stackalloc byte[MaxFormattedLength];
        return JsonNumberParts.Parse(GetUtf8(scratch)).TryGetInt64(out value);
    }

    /// <summary>
    /// Gets the value as a <see cref="decimal"/>. This fails if the number is out of range.
    /// Digits beyond the precision of <see cref="decimal"/> are rounded.
    /// </summary>
    public bool TryGetDecimal(out decimal value)
    {
        switch (kind)
        {
            case Kind.Int64:
                value = this.value.Int64;
                return true;
            case Kind.Decimal:
                value = this.value.Decimal;
                return true;
            default:
                Span<byte> scratch = stackalloc byte[MaxFormattedLength];
                return decimal.TryParse(GetUtf8(scratch), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    /// <summary>
    /// Gets the nearest <see cref="double"/>. This fails if the number is too large for a double.
    /// A number too small for a double gives zero.
    /// </summary>
    public bool TryGetDouble(out double value)
    {
        if (kind == Kind.Double)
        {
            value = this.value.Double;
            return true;
        }

        Span<byte> scratch = stackalloc byte[MaxFormattedLength];
        if (double.TryParse(GetUtf8(scratch), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Returns the number as a JSON element. A number from .NET creates a small document for it.
    /// </summary>
    public JsonElement ToJsonElement()
    {
        if (kind == Kind.Element)
        {
            return element;
        }

        Span<byte> scratch = stackalloc byte[MaxFormattedLength];
        var reader = new Utf8JsonReader(GetUtf8(scratch));
        return JsonElement.ParseValue(ref reader);
    }

    /// <summary>
    /// Writes the number.
    /// </summary>
    public void WriteTo(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        switch (kind)
        {
            case Kind.Int64:
                writer.WriteNumberValue(value.Int64);
                break;
            case Kind.Decimal:
                writer.WriteNumberValue(value.Decimal);
                break;
            case Kind.Double:
                writer.WriteNumberValue(value.Double);
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the two numbers have the same value, for example <c>1</c> and <c>1.0</c>.
    /// </summary>
    public bool Equals(JsonNumber other)
    {
        if (kind == Kind.Int64 && other.kind == Kind.Int64)
        {
            return value.Int64 == other.value.Int64;
        }

        Span<byte> scratch = stackalloc byte[MaxFormattedLength];
        Span<byte> otherScratch = stackalloc byte[MaxFormattedLength];
        var parts = JsonNumberParts.Parse(GetUtf8(scratch));
        return parts.ValueEquals(JsonNumberParts.Parse(other.GetUtf8(otherScratch)));
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is JsonNumber other && Equals(other);

    /// <summary>
    /// Returns a hash code. Two numbers with the same value have the same hash code.
    /// </summary>
    public override int GetHashCode()
    {
        Span<byte> scratch = stackalloc byte[MaxFormattedLength];
        return JsonNumberParts.Parse(GetUtf8(scratch)).GetValueHashCode();
    }

    /// <summary>
    /// Returns the number as JSON text.
    /// </summary>
    public override string ToString()
    {
        Span<byte> scratch = stackalloc byte[MaxFormattedLength];
        return Encoding.UTF8.GetString(GetUtf8(scratch));
    }

    /// <summary>
    /// Gets the JSON text of the number. A parsed number gives its own text. Other numbers are formatted into <paramref name="scratch"/>.
    /// </summary>
    internal ReadOnlySpan<byte> GetUtf8(Span<byte> scratch)
    {
        int written;
        switch (kind)
        {
            case Kind.Element:
                return JsonMarshal.GetRawUtf8Value(element);
            case Kind.Int64:
                value.Int64.TryFormat(scratch, out written, default, CultureInfo.InvariantCulture);
                break;
            case Kind.Decimal:
                value.Decimal.TryFormat(scratch, out written, default, CultureInfo.InvariantCulture);
                break;
            default:
                //  The default format is the shortest text that round-trips, for example 1E-05. It is valid JSON.
                value.Double.TryFormat(scratch, out written, default, CultureInfo.InvariantCulture);
                break;
        }

        return scratch[..written];
    }

    /// <summary>
    /// The numeric value of a number from .NET. The fields share memory, and <see cref="Kind"/> tells which one is set.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct NumericValue
    {
        [FieldOffset(0)]
        public long Int64;

        [FieldOffset(0)]
        public double Double;

        [FieldOffset(0)]
        public decimal Decimal;
    }

    private static bool IsSingleNumber(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8);
            return reader.Read() && reader.TokenType == JsonTokenType.Number && !reader.Read();
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
