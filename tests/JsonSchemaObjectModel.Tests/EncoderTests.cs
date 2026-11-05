using System.Text.Encodings.Web;
using System.Text.Json;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// ToJsonString escapes only what JSON requires. The other write paths use the encoder of the caller.
/// </summary>
public class EncoderTests
{
    private const string Json = """{"pattern":"^[a-z]+$","description":"ต้มยำ <b>&</b> 'x'"}""";

    private static readonly JsonSerializerOptions relaxedSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public void ToJsonString_keeps_patterns_and_non_ascii_text_readable()
    {
        Assert.Equal(Json, JsonSchema.Parse(Json).ToJsonString());
    }

    [Fact]
    public void ToJsonString_with_an_explicit_relaxed_encoder_gives_the_same_text()
    {
        var options = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        Assert.Equal(Json, JsonSchema.Parse(Json).ToJsonString(options));
    }

    [Fact]
    public void ToJsonString_with_the_default_encoder_escapes_html_and_non_ascii_text()
    {
        var json = JsonSchema.Parse(Json).ToJsonString(new JsonWriterOptions { Encoder = JavaScriptEncoder.Default });

        //  The default encoder escapes '+', HTML characters and non-ASCII text, so none of them appear as written.
        Assert.DoesNotContain("+", json, StringComparison.Ordinal);
        Assert.DoesNotContain("<", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ต้มยำ", json, StringComparison.Ordinal);
        AssertSemanticallyEqual(Json, json);
    }

    [Fact]
    public void The_converter_uses_the_encoder_of_the_serializer_options()
    {
        var schema = JsonSchema.Parse(Json);

        var withDefaultOptions = JsonSerializer.Serialize(schema);

        Assert.DoesNotContain("+", withDefaultOptions, StringComparison.Ordinal);
        AssertSemanticallyEqual(Json, withDefaultOptions);
        Assert.Equal(Json, JsonSerializer.Serialize(schema, relaxedSerializerOptions));
    }

    private static void AssertSemanticallyEqual(string expected, string actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Assert.True(JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement), actual);
    }
}
