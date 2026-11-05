using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace JsonSchemaObjectModel.Keywords;

/// <summary>
/// The <c>type</c> keyword.
/// </summary>
/// <remarks>
/// The JSON value is a string or an array of strings.
/// The keyword keeps that form and the order, so the schema round-trips.
/// </remarks>
public sealed class TypeKeyword : IJsonSchemaKeyword
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "type";

    private static readonly SchemaValueType[] allTypes =
    [
        SchemaValueType.Object,
        SchemaValueType.Array,
        SchemaValueType.Boolean,
        SchemaValueType.String,
        SchemaValueType.Number,
        SchemaValueType.Integer,
        SchemaValueType.Null,
    ];

    //  Static fields initialize in order. This one uses allTypes, so it must come after it.
    private static readonly TypeKeyword[] singles =
    [
        new(SchemaValueType.Object),
        new(SchemaValueType.Array),
        new(SchemaValueType.Boolean),
        new(SchemaValueType.String),
        new(SchemaValueType.Number),
        new(SchemaValueType.Integer),
        new(SchemaValueType.Null),
    ];

    /// <summary>
    /// Creates the keyword from one or more flags.
    /// One flag writes a string. More than one flag writes an array.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="type"/> is <see cref="SchemaValueType.None"/> or has unknown flags.</exception>
    public TypeKeyword(SchemaValueType type)
    {
        var builder = ImmutableArray.CreateBuilder<SchemaValueType>();
        foreach (var candidate in allTypes)
        {
            if ((type & candidate) != 0)
            {
                builder.Add(candidate);
            }
        }

        if (builder.Count == 0 || (type & ~AllFlags) != 0)
        {
            throw new ArgumentException("The type must be one or more known flags.", nameof(type));
        }

        Types = builder.ToImmutable();
        Type = type;
        IsArray = Types.Length > 1;
    }

    /// <summary>
    /// Creates the keyword from a list of single types. The keyword writes an array.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="types"/> is empty, or a value is not a single known flag, or a value occurs more than once.
    /// </exception>
    public TypeKeyword(IEnumerable<SchemaValueType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        Types = [.. types];
        if (Types.IsEmpty)
        {
            throw new ArgumentException("The list must contain at least one type.", nameof(types));
        }

        foreach (var type in Types)
        {
            if (Array.IndexOf(allTypes, type) < 0)
            {
                throw new ArgumentException($"'{type}' is not a single known type.", nameof(types));
            }

            if ((Type & type) != 0)
            {
                throw new ArgumentException($"The type '{type}' occurs more than once.", nameof(types));
            }

            Type |= type;
        }

        IsArray = true;
    }

    /// <inheritdoc/>
    public string Name => KeywordName;

    /// <summary>
    /// Returns a shared keyword for one or more flags. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static TypeKeyword For(SchemaValueType type)
    {
        var index = Array.IndexOf(allTypes, type);
        return index >= 0 ? singles[index] : new TypeKeyword(type);
    }

    /// <summary>
    /// All the types, as flags.
    /// </summary>
    public SchemaValueType Type { get; }

    /// <summary>
    /// The types, in document order.
    /// </summary>
    public ImmutableArray<SchemaValueType> Types { get; }

    /// <summary>
    /// <see langword="true"/> if the JSON value is an array.
    /// </summary>
    public bool IsArray { get; }

    private static SchemaValueType AllFlags =>
        SchemaValueType.Object
        | SchemaValueType.Array
        | SchemaValueType.Boolean
        | SchemaValueType.String
        | SchemaValueType.Number
        | SchemaValueType.Integer
        | SchemaValueType.Null;

    /// <summary>
    /// Returns the JSON name of a single type, for example <c>"string"</c>.
    /// </summary>
    public static string GetTypeName(SchemaValueType type) =>
        type switch
        {
            SchemaValueType.Object => "object",
            SchemaValueType.Array => "array",
            SchemaValueType.Boolean => "boolean",
            SchemaValueType.String => "string",
            SchemaValueType.Number => "number",
            SchemaValueType.Integer => "integer",
            SchemaValueType.Null => "null",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "The value must be a single known type."),
        };

    /// <summary>
    /// Gets the type that a JSON string names, without allocating a string.
    /// </summary>
    public static bool TryParseTypeName(JsonElement name, out SchemaValueType type)
    {
        type = SchemaValueType.None;
        if (name.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        foreach (var candidate in allTypes)
        {
            if (name.ValueEquals(GetTypeName(candidate)))
            {
                type = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the type that has a JSON name, for example <c>"string"</c>.
    /// </summary>
    public static bool TryParseTypeName(string? name, out SchemaValueType type)
    {
        type = name switch
        {
            "object" => SchemaValueType.Object,
            "array" => SchemaValueType.Array,
            "boolean" => SchemaValueType.Boolean,
            "string" => SchemaValueType.String,
            "number" => SchemaValueType.Number,
            "integer" => SchemaValueType.Integer,
            "null" => SchemaValueType.Null,
            _ => SchemaValueType.None,
        };
        return type != SchemaValueType.None;
    }

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        if (!IsArray)
        {
            writer.WriteStringValue(GetTypeName(Types[0]));
            return;
        }

        writer.WriteStartArray();
        foreach (var type in Types)
        {
            writer.WriteStringValue(GetTypeName(type));
        }

        writer.WriteEndArray();
    }
}

/// <summary>The <c>enum</c> keyword.</summary>
public sealed class EnumKeyword : JsonValueArrayKeyword
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "enum";

    /// <summary>Creates the keyword.</summary>
    public EnumKeyword(IEnumerable<JsonElement> values)
        : base(values) { }

    internal EnumKeyword(ImmutableArray<JsonElement> clonedValues)
        : base(clonedValues) { }

    internal EnumKeyword(ImmutableArray<string> strings)
        : base(strings) { }

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>const</c> keyword.</summary>
public sealed class ConstKeyword : JsonValueKeyword
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "const";

    /// <summary>Creates the keyword.</summary>
    public ConstKeyword(JsonElement value)
        : base(value) { }

    internal ConstKeyword(PrimitiveJsonValue value)
        : base(value) { }

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>multipleOf</c> keyword. The value must be greater than zero.</summary>
public sealed class MultipleOfKeyword : NumberValueKeyword
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "multipleOf";

    /// <summary>
    /// Creates the keyword.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not greater than zero.</exception>
    public MultipleOfKeyword(JsonElement value)
        : base(value)
    {
        if (!(GetDouble() > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The value must be greater than zero.");
        }
    }

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>maximum</c> keyword.</summary>
public sealed class MaximumKeyword(JsonElement value) : NumberValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "maximum";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>exclusiveMaximum</c> keyword.</summary>
public sealed class ExclusiveMaximumKeyword(JsonElement value) : NumberValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "exclusiveMaximum";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>minimum</c> keyword.</summary>
public sealed class MinimumKeyword(JsonElement value) : NumberValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "minimum";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>exclusiveMinimum</c> keyword.</summary>
public sealed class ExclusiveMinimumKeyword(JsonElement value) : NumberValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "exclusiveMinimum";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>maxLength</c> keyword.</summary>
public sealed class MaxLengthKeyword(ulong value) : NonNegativeIntegerKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "maxLength";

    private static readonly MaxLengthKeyword[] shared = SharedKeywords.CreateIntegerCache(static value => new MaxLengthKeyword(value));

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static MaxLengthKeyword For(ulong value) => value < (ulong)shared.Length ? shared[value] : new(value);

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>minLength</c> keyword.</summary>
public sealed class MinLengthKeyword(ulong value) : NonNegativeIntegerKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "minLength";

    private static readonly MinLengthKeyword[] shared = SharedKeywords.CreateIntegerCache(static value => new MinLengthKeyword(value));

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static MinLengthKeyword For(ulong value) => value < (ulong)shared.Length ? shared[value] : new(value);

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>pattern</c> keyword. The value is an ECMA-262 regular expression.</summary>
public sealed class PatternKeyword(string value) : StringValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "pattern";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>maxItems</c> keyword.</summary>
public sealed class MaxItemsKeyword(ulong value) : NonNegativeIntegerKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "maxItems";

    private static readonly MaxItemsKeyword[] shared = SharedKeywords.CreateIntegerCache(static value => new MaxItemsKeyword(value));

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static MaxItemsKeyword For(ulong value) => value < (ulong)shared.Length ? shared[value] : new(value);

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>minItems</c> keyword.</summary>
public sealed class MinItemsKeyword(ulong value) : NonNegativeIntegerKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "minItems";

    private static readonly MinItemsKeyword[] shared = SharedKeywords.CreateIntegerCache(static value => new MinItemsKeyword(value));

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static MinItemsKeyword For(ulong value) => value < (ulong)shared.Length ? shared[value] : new(value);

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>uniqueItems</c> keyword.</summary>
public sealed class UniqueItemsKeyword(bool value) : BooleanValueKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "uniqueItems";

    private static readonly UniqueItemsKeyword sharedTrue = new(true);
    private static readonly UniqueItemsKeyword sharedFalse = new(false);

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static UniqueItemsKeyword For(bool value) => value ? sharedTrue : sharedFalse;

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>maxContains</c> keyword.</summary>
public sealed class MaxContainsKeyword(ulong value) : NonNegativeIntegerKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "maxContains";

    private static readonly MaxContainsKeyword[] shared = SharedKeywords.CreateIntegerCache(static value => new MaxContainsKeyword(value));

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static MaxContainsKeyword For(ulong value) => value < (ulong)shared.Length ? shared[value] : new(value);

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>minContains</c> keyword.</summary>
public sealed class MinContainsKeyword(ulong value) : NonNegativeIntegerKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "minContains";

    private static readonly MinContainsKeyword[] shared = SharedKeywords.CreateIntegerCache(static value => new MinContainsKeyword(value));

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static MinContainsKeyword For(ulong value) => value < (ulong)shared.Length ? shared[value] : new(value);

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>maxProperties</c> keyword.</summary>
public sealed class MaxPropertiesKeyword(ulong value) : NonNegativeIntegerKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "maxProperties";

    private static readonly MaxPropertiesKeyword[] shared = SharedKeywords.CreateIntegerCache(static value => new MaxPropertiesKeyword(value));

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static MaxPropertiesKeyword For(ulong value) => value < (ulong)shared.Length ? shared[value] : new(value);

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>minProperties</c> keyword.</summary>
public sealed class MinPropertiesKeyword(ulong value) : NonNegativeIntegerKeyword(value)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "minProperties";

    private static readonly MinPropertiesKeyword[] shared = SharedKeywords.CreateIntegerCache(static value => new MinPropertiesKeyword(value));

    /// <summary>
    /// Returns a shared keyword when one exists for the value, or a new one. The keyword is immutable, so it is safe to share.
    /// </summary>
    public static MinPropertiesKeyword For(ulong value) => value < (ulong)shared.Length ? shared[value] : new(value);

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>required</c> keyword.</summary>
public sealed class RequiredKeyword : IJsonSchemaKeyword
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "required";

    /// <summary>
    /// Creates the keyword.
    /// </summary>
    /// <exception cref="ArgumentException">A property name occurs more than once.</exception>
    public RequiredKeyword(IEnumerable<string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        Properties = UniqueStrings.Create(properties, nameof(properties));
    }

    /// <inheritdoc/>
    public string Name => KeywordName;

    /// <summary>
    /// The names of the required properties, in document order.
    /// </summary>
    public ImmutableArray<string> Properties { get; }

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer) => UniqueStrings.Write(writer, Properties);
}

/// <summary>The <c>dependentRequired</c> keyword.</summary>
public sealed class DependentRequiredKeyword : IJsonSchemaKeyword
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "dependentRequired";

    /// <summary>
    /// Creates the keyword. The order of <paramref name="dependencies"/> is kept.
    /// </summary>
    /// <exception cref="ArgumentException">A name occurs more than once.</exception>
    public DependentRequiredKeyword(IEnumerable<KeyValuePair<string, IEnumerable<string>>> dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        var ordered = new OrderedDictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
        foreach (var (property, required) in dependencies)
        {
            if (!ordered.TryAdd(property, UniqueStrings.Create(required, nameof(dependencies))))
            {
                throw new ArgumentException($"The property '{property}' occurs more than once.", nameof(dependencies));
            }
        }

        Dependencies = new ReadOnlyDictionary<string, ImmutableArray<string>>(ordered);
    }

    /// <inheritdoc/>
    public string Name => KeywordName;

    /// <summary>
    /// For each property name, the properties that are required when that property is present.
    /// </summary>
    public IReadOnlyDictionary<string, ImmutableArray<string>> Dependencies { get; }

    /// <inheritdoc/>
    public void WriteValue(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (property, required) in Dependencies)
        {
            writer.WritePropertyName(property);
            UniqueStrings.Write(writer, required);
        }

        writer.WriteEndObject();
    }
}

internal static class UniqueStrings
{
    public static ImmutableArray<string> Create(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var result = values is ImmutableArray<string> immutable ? immutable : ImmutableArray.CreateRange(values);
        for (var position = 0; position < result.Length; position++)
        {
            ArgumentNullException.ThrowIfNull(result[position], parameterName);
            if (IndexOf(result, result[position], position) >= 0)
            {
                throw new ArgumentException($"The value '{result[position]}' occurs more than once.", parameterName);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the index of <paramref name="value"/> in the first <paramref name="count"/> items, or -1.
    /// </summary>
    /// <remarks>
    /// The arrays in a schema are small, so a linear search is faster than a hash set and does not allocate.
    /// </remarks>
    public static int IndexOf(ImmutableArray<string> values, string value, int count)
    {
        for (var position = 0; position < count; position++)
        {
            if (string.Equals(values[position], value, StringComparison.Ordinal))
            {
                return position;
            }
        }

        return -1;
    }

    public static void Write(Utf8JsonWriter writer, ImmutableArray<string> values)
    {
        writer.WriteStartArray();
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}
