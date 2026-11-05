using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonSchemaObjectModel;

/// <summary>
/// Reads and writes <see cref="JsonSchema"/> values with System.Text.Json.
/// </summary>
/// <remarks>
/// <see cref="JsonSchema"/> has this converter by default, with <see cref="JsonSchemaReaderOptions.Default"/>.
/// To read custom keywords, add an instance with your own options to <see cref="JsonSerializerOptions.Converters"/>.
/// </remarks>
public sealed class JsonSchemaConverter : JsonConverter<JsonSchema>
{
    private readonly JsonSchemaReaderOptions readerOptions;

    /// <summary>
    /// Creates a converter that reads with <see cref="JsonSchemaReaderOptions.Default"/>.
    /// </summary>
    public JsonSchemaConverter()
        : this(JsonSchemaReaderOptions.Default) { }

    /// <summary>
    /// Creates a converter that reads with the given options.
    /// </summary>
    public JsonSchemaConverter(JsonSchemaReaderOptions readerOptions)
    {
        ArgumentNullException.ThrowIfNull(readerOptions);
        this.readerOptions = readerOptions;
    }

    /// <inheritdoc/>
    public override JsonSchema Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        //  ParseValue gives an element that owns its memory, so keywords keep JSON values without copying them.
        return JsonSchema.FromJsonElement(JsonElement.ParseValue(ref reader), readerOptions);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, JsonSchema value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}
