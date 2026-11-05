using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace JsonSchemaObjectModel;

/// <summary>
/// The parts of a JSON number, read from its UTF-8 text without a conversion to a binary type.
/// </summary>
/// <remarks>
/// - The value is <c>significand × 10^scale</c>. The significand is a run of decimal digits with no leading
///   or trailing zeros. It can be split between the integer digits and the fraction digits of the text.
/// - Two numbers are equal if, and only if, they have the same sign, the same significand and the same scale.
///   Every zero is equal, and has no sign.
/// - The type reads valid JSON number text only. The caller must make sure of that.
/// </remarks>
internal readonly ref struct JsonNumberParts
{
    //  An exponent with more digits than this does not fit a long. Such an exponent uses BigInteger.
    private const int MaxLongExponentDigits = 17;

    private readonly ReadOnlySpan<byte> integerDigits;
    private readonly ReadOnlySpan<byte> fractionDigits;
    private readonly ReadOnlySpan<byte> exponentDigits;
    private readonly bool exponentNegative;

    //  The significand is digits [first, last) of integerDigits followed by fractionDigits.
    private readonly int first;
    private readonly int last;

    private JsonNumberParts(
        bool negative,
        ReadOnlySpan<byte> integerDigits,
        ReadOnlySpan<byte> fractionDigits,
        bool exponentNegative,
        ReadOnlySpan<byte> exponentDigits
    )
    {
        this.integerDigits = integerDigits;
        this.fractionDigits = fractionDigits;
        this.exponentNegative = exponentNegative;
        this.exponentDigits = exponentDigits;

        var length = integerDigits.Length + fractionDigits.Length;
        var start = 0;
        while (start < length && DigitAt(start) == 0)
        {
            start++;
        }

        var end = length;
        while (end > start && DigitAt(end - 1) == 0)
        {
            end--;
        }

        first = start;
        last = end;
        IsNegative = negative && !IsZero;
    }

    /// <summary>
    /// <see langword="true"/> if the number is less than zero.
    /// </summary>
    public bool IsNegative { get; }

    /// <summary>
    /// <see langword="true"/> if the number is zero, for example <c>0</c>, <c>-0.0</c> or <c>0e10</c>.
    /// </summary>
    public bool IsZero => first == last;

    /// <summary>
    /// -1, 0 or 1.
    /// </summary>
    public int Sign => IsZero ? 0 : IsNegative ? -1 : 1;

    /// <summary>
    /// The number of digits in the significand.
    /// </summary>
    public int SignificandLength => last - first;

    /// <summary>
    /// <see langword="true"/> if the number has no fractional part, for example <c>2.0</c> or <c>1.5e1</c>.
    /// </summary>
    public bool IsInteger => IsZero || ScaleSign() >= 0;

    /// <summary>
    /// Reads the parts of the raw text of a number element.
    /// </summary>
    public static JsonNumberParts From(JsonElement number) => Parse(JsonMarshal.GetRawUtf8Value(number));

    /// <summary>
    /// Reads the parts of valid JSON number text.
    /// </summary>
    public static JsonNumberParts Parse(ReadOnlySpan<byte> utf8)
    {
        var position = 0;
        var negative = utf8[0] == (byte)'-';
        if (negative)
        {
            position++;
        }

        var integerStart = position;
        while (position < utf8.Length && char.IsAsciiDigit((char)utf8[position]))
        {
            position++;
        }

        var integerDigits = utf8[integerStart..position];
        var fractionDigits = ReadOnlySpan<byte>.Empty;
        if (position < utf8.Length && utf8[position] == (byte)'.')
        {
            var fractionStart = ++position;
            while (position < utf8.Length && char.IsAsciiDigit((char)utf8[position]))
            {
                position++;
            }

            fractionDigits = utf8[fractionStart..position];
        }

        var exponentNegative = false;
        var exponentDigits = ReadOnlySpan<byte>.Empty;
        if (position < utf8.Length && (utf8[position] | 0x20) == (byte)'e')
        {
            position++;
            if (utf8[position] is (byte)'-' or (byte)'+')
            {
                exponentNegative = utf8[position] == (byte)'-';
                position++;
            }

            exponentDigits = utf8[position..].TrimStart((byte)'0');
        }

        return new JsonNumberParts(negative, integerDigits, fractionDigits, exponentNegative, exponentDigits);
    }

    /// <summary>
    /// Gets the significand digit at a position, from 0 to <see cref="SignificandLength"/> - 1.
    /// </summary>
    public int SignificandDigit(int position) => DigitAt(first + position);

    /// <summary>
    /// Gets the scale, the power of ten that multiplies the significand.
    /// </summary>
    /// <returns><see langword="false"/> if the scale does not fit a <see cref="long"/>.</returns>
    public bool TryGetScale(out long scale)
    {
        scale = 0;
        if (exponentDigits.Length > MaxLongExponentDigits)
        {
            return false;
        }

        long exponent = 0;
        foreach (var digit in exponentDigits)
        {
            exponent = (exponent * 10) + (digit - '0');
        }

        //  A text has fewer than int.MaxValue digits, so these terms cannot overflow a long.
        scale = (exponentNegative ? -exponent : exponent) - fractionDigits.Length + TrailingZeros();
        return true;
    }

    /// <summary>
    /// Gets the scale for any exponent. This allocates, so use it only if <see cref="TryGetScale"/> fails.
    /// </summary>
    public BigInteger GetBigScale()
    {
        var exponent = BigInteger.Zero;
        foreach (var digit in exponentDigits)
        {
            exponent = (exponent * 10) + (digit - '0');
        }

        return (exponentNegative ? -exponent : exponent) - fractionDigits.Length + TrailingZeros();
    }

    /// <summary>
    /// Gets the value as a <see cref="ulong"/> if it is a non-negative integer that fits.
    /// </summary>
    public bool TryGetUInt64(out ulong value)
    {
        value = 0;
        return !IsNegative && TryGetMagnitude(out value);
    }

    /// <summary>
    /// Gets the value as a <see cref="long"/> if it is an integer that fits.
    /// </summary>
    public bool TryGetInt64(out long value)
    {
        value = 0;
        if (!TryGetMagnitude(out var magnitude))
        {
            return false;
        }

        //  The magnitude of long.MinValue is one more than long.MaxValue.
        var limit = IsNegative ? (ulong)long.MaxValue + 1 : long.MaxValue;
        if (magnitude > limit)
        {
            return false;
        }

        value = !IsNegative ? (long)magnitude
            : magnitude == limit ? long.MinValue
            : -(long)magnitude;
        return true;
    }

    //  Gets the absolute value as a ulong if the number is an integer that fits.
    private bool TryGetMagnitude(out ulong value)
    {
        value = 0;
        if (IsZero)
        {
            return true;
        }

        if (!TryGetScale(out var scale) || scale < 0 || SignificandLength + scale > 20)
        {
            return false;
        }

        try
        {
            checked
            {
                for (var position = 0; position < SignificandLength; position++)
                {
                    value = (value * 10) + (ulong)SignificandDigit(position);
                }

                for (var power = 0L; power < scale; power++)
                {
                    value *= 10;
                }
            }

            return true;
        }
        catch (OverflowException)
        {
            value = 0;
            return false;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if two numbers have the same value.
    /// </summary>
    public bool ValueEquals(in JsonNumberParts other)
    {
        if (IsZero || other.IsZero)
        {
            return IsZero && other.IsZero;
        }

        if (IsNegative != other.IsNegative || SignificandLength != other.SignificandLength)
        {
            return false;
        }

        for (var position = 0; position < SignificandLength; position++)
        {
            if (SignificandDigit(position) != other.SignificandDigit(position))
            {
                return false;
            }
        }

        if (TryGetScale(out var scale) && other.TryGetScale(out var otherScale))
        {
            return scale == otherScale;
        }

        return GetBigScale() == other.GetBigScale();
    }

    /// <summary>
    /// Returns a hash code. Two numbers with the same value have the same hash code.
    /// </summary>
    public int GetValueHashCode()
    {
        if (IsZero)
        {
            return 0;
        }

        var hash = new HashCode();
        hash.Add(IsNegative);
        for (var position = 0; position < SignificandLength; position++)
        {
            hash.Add(SignificandDigit(position));
        }

        //  A long exponent can still give a scale that fits a long. Add it as a long, so that the hash
        //  is the same as for the same scale from a short exponent.
        if (TryGetScale(out var scale))
        {
            hash.Add(scale);
        }
        else
        {
            var bigScale = GetBigScale();
            if (bigScale >= long.MinValue && bigScale <= long.MaxValue)
            {
                hash.Add((long)bigScale);
            }
            else
            {
                hash.Add(bigScale);
            }
        }

        return hash.ToHashCode();
    }

    private int DigitAt(int position) =>
        (position < integerDigits.Length ? integerDigits[position] : fractionDigits[position - integerDigits.Length]) - '0';

    private int TrailingZeros() => integerDigits.Length + fractionDigits.Length - last;

    //  The sign of the scale, also for an exponent that does not fit a long.
    private int ScaleSign() => TryGetScale(out var scale) ? Math.Sign(scale) : GetBigScale().Sign;
}
