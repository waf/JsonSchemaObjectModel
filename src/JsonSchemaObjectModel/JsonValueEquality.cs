using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel;

/// <summary>
/// Equality and hash codes for JSON values. Schema equality uses these rules.
/// </summary>
/// <remarks>
/// - The rules are those of <see cref="JsonElement.DeepEquals"/>:
///   - Numbers are equal if their values are equal, so <c>1</c> equals <c>1.0</c>.
///   - Strings and property names are compared after unescaping.
///   - The order of object properties is not important. The order of array items is important.
/// - Numbers use <see cref="JsonNumberParts"/>, so an exponent beyond <see cref="int"/> works.
///   <see cref="JsonElement.DeepEquals"/> throws for such an exponent.
/// - A typed keyword must give the same hash code as the JSON that it writes. The hash helpers here make that easy:
///   use <see cref="String(string)"/>, <see cref="Boolean"/>, <see cref="Number(JsonNumber)"/>,
///   <see cref="ArrayHash"/> and <see cref="ObjectHash"/> in the same way as <see cref="Element"/> does.
/// </remarks>
internal static class JsonValueEquality
{
    //  Each kind of value has a tag, so that, for example, the string "1" and the number 1 do not collide.
    private const int NullHash = 0x2545F491;
    private const int TrueHash = 0x4F6CDD1D;
    private const int FalseHash = 0x6B43A9B5;
    private const int StringTag = 0x1B873593;
    private const int ArrayTag = 0x3C6EF372;
    private const int ObjectTag = 0x5BD1E995;

    //  Names and strings up to this length are unescaped on the stack.
    private const int MaxStackChars = 256;

    public static int Null => NullHash;

    public static int Boolean(bool value) => value ? TrueHash : FalseHash;

    public static int String(string value) => String(value.AsSpan());

    public static int String(ReadOnlySpan<char> value) => HashCode.Combine(StringTag, string.GetHashCode(value));

    public static int Number(JsonNumber value) => value.GetHashCode();

    public static int Number(ulong value) => JsonNumber.From((decimal)value).GetHashCode();

    /// <summary>
    /// Starts the hash of an array. Add each item with <see cref="HashCode.Add{T}(T)"/>, then call <see cref="ArrayHash"/>.
    /// </summary>
    public static HashCode StartArray()
    {
        var hash = new HashCode();
        hash.Add(ArrayTag);
        return hash;
    }

    public static int ArrayHash(HashCode items, int count)
    {
        items.Add(count);
        return items.ToHashCode();
    }

    /// <summary>
    /// The hash of one property. Add the results for all properties with unchecked addition, then call <see cref="ObjectHash"/>.
    /// The sum does not depend on the order of the properties.
    /// </summary>
    public static int Property(int nameHash, int valueHash) => HashCode.Combine(nameHash, valueHash);

    public static int ObjectHash(int propertySum, int count) => HashCode.Combine(ObjectTag, propertySum, count);

    public static int StringArray(ImmutableArray<string> values)
    {
        var hash = StartArray();
        foreach (var value in values)
        {
            hash.Add(String(value));
        }

        return ArrayHash(hash, values.Length);
    }

    /// <summary>
    /// Returns the hash code of a JSON value.
    /// </summary>
    public static int Element(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return NullHash;
            case JsonValueKind.True:
                return TrueHash;
            case JsonValueKind.False:
                return FalseHash;
            case JsonValueKind.Number:
                return JsonNumberParts.From(element).GetValueHashCode();
            case JsonValueKind.String:
                return StringElement(element);
            case JsonValueKind.Array:
                var items = StartArray();
                foreach (var item in element.EnumerateArray())
                {
                    items.Add(Element(item));
                }

                return ArrayHash(items, element.GetArrayLength());
            default:
                var sum = 0;
                foreach (var property in element.EnumerateObject())
                {
                    sum = unchecked(sum + Property(PropertyName(property), Element(property.Value)));
                }

                return ObjectHash(sum, element.GetPropertyCount());
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if two JSON values are equal.
    /// </summary>
    /// <remarks>
    /// An object with the same property name more than once is compared by its last value for each name.
    /// JSON does not define such objects, and schemas do not use them.
    /// </remarks>
    public static bool ElementEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Number:
                return JsonNumberParts.From(left).ValueEquals(JsonNumberParts.From(right));
            case JsonValueKind.String:
                return StringElementsEqual(left, right);
            case JsonValueKind.Array:
                if (left.GetArrayLength() != right.GetArrayLength())
                {
                    return false;
                }

                using (var rightItems = right.EnumerateArray())
                {
                    foreach (var leftItem in left.EnumerateArray())
                    {
                        rightItems.MoveNext();
                        if (!ElementEquals(leftItem, rightItems.Current))
                        {
                            return false;
                        }
                    }
                }

                return true;
            case JsonValueKind.Object:
                if (left.GetPropertyCount() != right.GetPropertyCount())
                {
                    return false;
                }

                foreach (var property in left.EnumerateObject())
                {
                    var rawName = JsonMarshal.GetRawUtf8PropertyName(property);
                    var found = rawName.Contains((byte)'\\')
                        ? right.TryGetProperty(property.Name, out var rightValue)
                        : right.TryGetProperty(rawName, out rightValue);
                    if (!found || !ElementEquals(property.Value, rightValue))
                    {
                        return false;
                    }
                }

                return true;
            default:
                //  Null, true and false: the kinds are equal, so the values are equal.
                return true;
        }
    }

    /// <summary>
    /// Returns a keyword value as a JSON element. An <see cref="UnknownKeyword"/> gives its element. Other keywords
    /// write their value into a new document. Use this only for keywords that have no typed comparison.
    /// </summary>
    public static JsonElement KeywordValue(IJsonSchemaKeyword keyword)
    {
        if (keyword is UnknownKeyword unknown)
        {
            return unknown.Value;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            keyword.WriteValue(writer);
        }

        var reader = new Utf8JsonReader(buffer.WrittenSpan);
        return JsonElement.ParseValue(ref reader);
    }

    private static int StringElement(JsonElement element)
    {
        var raw = JsonMarshal.GetRawUtf8Value(element);

        //  The raw value has its quotes. Without escapes, the text between them is the value.
        return HashUtf8OrString(raw[1..^1], element);
    }

    private static int PropertyName(JsonProperty property) =>
        HashUtf8OrString(JsonMarshal.GetRawUtf8PropertyName(property), property);

    private static int HashUtf8OrString(ReadOnlySpan<byte> raw, JsonElement element) =>
        raw.Contains((byte)'\\') || raw.Length > MaxStackChars ? String(element.GetString()!) : HashUtf8(raw);

    private static int HashUtf8OrString(ReadOnlySpan<byte> raw, JsonProperty property) =>
        raw.Contains((byte)'\\') || raw.Length > MaxStackChars ? String(property.Name) : HashUtf8(raw);

    private static int HashUtf8(ReadOnlySpan<byte> utf8)
    {
        Span<char> chars = stackalloc char[MaxStackChars];
        var length = Encoding.UTF8.GetChars(utf8, chars);
        return String(chars[..length]);
    }

    private static bool StringElementsEqual(JsonElement left, JsonElement right)
    {
        var leftRaw = JsonMarshal.GetRawUtf8Value(left);
        var rightRaw = JsonMarshal.GetRawUtf8Value(right);
        if (!leftRaw.Contains((byte)'\\') && !rightRaw.Contains((byte)'\\'))
        {
            return leftRaw.SequenceEqual(rightRaw);
        }

        return left.ValueEquals(right.GetString());
    }
}
