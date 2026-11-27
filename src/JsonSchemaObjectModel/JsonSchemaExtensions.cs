using System.Collections.Immutable;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel;

/// <summary>
/// Short accessors for frequently used keywords.
/// </summary>
/// <remarks>
/// Each accessor returns <see langword="null"/> if the schema does not have the keyword.
/// For other keywords, use <see cref="JsonSchema.GetKeyword{T}"/>.
/// </remarks>
public static class JsonSchemaExtensions
{
    extension(JsonSchema schema)
    {
        /// <summary>The value of <c>properties</c>.</summary>
        public JsonSchemaMap? Properties => schema.GetKeyword<PropertiesKeyword>()?.Schemas;

        /// <summary>The value of <c>prefixItems</c>.</summary>
        public ImmutableArray<JsonSchema>? PrefixItems => schema.GetKeyword<PrefixItemsKeyword>()?.Schemas;

        /// <summary>The value of <c>items</c>.</summary>
        public JsonSchema? Items => schema.GetKeyword<ItemsKeyword>()?.Schema;

        /// <summary>The value of <c>required</c>.</summary>
        public ImmutableArray<string>? Required => schema.GetKeyword<RequiredKeyword>()?.Names;

        /// <summary>The value of <c>type</c>, as flags.</summary>
        public JsonSchemaType? Type => schema.GetKeyword<TypeKeyword>()?.Flags;

        /// <summary>The value of <c>format</c>.</summary>
        public string? Format => schema.GetKeyword<FormatKeyword>()?.Value;

        /// <summary>The value of <c>$defs</c>.</summary>
        public JsonSchemaMap? Defs => schema.GetKeyword<DefsKeyword>()?.Schemas;
    }
}
