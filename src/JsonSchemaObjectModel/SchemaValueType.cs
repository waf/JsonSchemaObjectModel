namespace JsonSchemaObjectModel;

/// <summary>
/// The JSON Schema primitive types that the <c>type</c> keyword names.
/// </summary>
[Flags]
public enum SchemaValueType
{
    /// <summary>No type.</summary>
    None = 0,

    /// <summary>A JSON object.</summary>
    Object = 1,

    /// <summary>A JSON array.</summary>
    Array = 2,

    /// <summary>A JSON boolean.</summary>
    Boolean = 4,

    /// <summary>A JSON string.</summary>
    String = 8,

    /// <summary>A JSON number.</summary>
    Number = 16,

    /// <summary>A JSON number with a zero fractional part.</summary>
    Integer = 32,

    /// <summary>The JSON null value.</summary>
    Null = 64,
}
