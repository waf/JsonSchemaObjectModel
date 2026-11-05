namespace JsonSchemaObjectModel.Keywords;

/// <summary>The <c>allOf</c> keyword.</summary>
public sealed class AllOfKeyword(IEnumerable<JsonSchema> schemas) : SubschemaArrayKeyword(schemas)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "allOf";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>anyOf</c> keyword.</summary>
public sealed class AnyOfKeyword(IEnumerable<JsonSchema> schemas) : SubschemaArrayKeyword(schemas)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "anyOf";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>oneOf</c> keyword.</summary>
public sealed class OneOfKeyword(IEnumerable<JsonSchema> schemas) : SubschemaArrayKeyword(schemas)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "oneOf";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>not</c> keyword.</summary>
public sealed class NotKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "not";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>if</c> keyword.</summary>
public sealed class IfKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "if";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>then</c> keyword.</summary>
public sealed class ThenKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "then";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>else</c> keyword.</summary>
public sealed class ElseKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "else";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>dependentSchemas</c> keyword.</summary>
public sealed class DependentSchemasKeyword(IEnumerable<KeyValuePair<string, JsonSchema>> schemas)
    : SubschemaMapKeyword(schemas)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "dependentSchemas";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>prefixItems</c> keyword.</summary>
public sealed class PrefixItemsKeyword(IEnumerable<JsonSchema> schemas) : SubschemaArrayKeyword(schemas)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "prefixItems";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>items</c> keyword.</summary>
public sealed class ItemsKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "items";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>contains</c> keyword.</summary>
public sealed class ContainsKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "contains";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>properties</c> keyword.</summary>
public sealed class PropertiesKeyword(IEnumerable<KeyValuePair<string, JsonSchema>> schemas) : SubschemaMapKeyword(schemas)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "properties";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>patternProperties</c> keyword.</summary>
public sealed class PatternPropertiesKeyword(IEnumerable<KeyValuePair<string, JsonSchema>> schemas)
    : SubschemaMapKeyword(schemas)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "patternProperties";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>additionalProperties</c> keyword.</summary>
public sealed class AdditionalPropertiesKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "additionalProperties";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>propertyNames</c> keyword.</summary>
public sealed class PropertyNamesKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "propertyNames";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>unevaluatedItems</c> keyword.</summary>
public sealed class UnevaluatedItemsKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "unevaluatedItems";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}

/// <summary>The <c>unevaluatedProperties</c> keyword.</summary>
public sealed class UnevaluatedPropertiesKeyword(JsonSchema schema) : SingleSubschemaKeyword(schema)
{
    /// <summary>The keyword name.</summary>
    public const string KeywordName = "unevaluatedProperties";

    /// <inheritdoc/>
    public override string Name => KeywordName;
}
