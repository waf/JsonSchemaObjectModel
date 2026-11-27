using System.Text.Json;
using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

public class JsonNumberTests
{
    //  Each group holds numbers with the same value, in different forms.
    private static readonly string[][] groups =
    [
        ["1", "1.0", "1e0", "10e-1", "0.1e1", "1.000E+0", "100e-2"],
        ["0", "-0", "0.0", "-0.0e5", "0e999999999999999999999"],
        ["-1.5", "-15e-1", "-0.15E1"],
        ["1e400", "10e399", "0.1e401"],
        ["1e-400", "0.1e-399"],
        ["1e100000000000000000", "10e99999999999999999"],
        ["1e100000000000000000000000000000", "10e99999999999999999999999999999"],
        ["1e-100000000000000000000000000000", "0.1e-99999999999999999999999999999"],
        ["18446744073709551616", "1.8446744073709551616e19"],
    ];

    public static TheoryData<string[]> EqualGroups => new(groups);

    [Theory]
    [MemberData(nameof(EqualGroups))]
    public void Numbers_with_the_same_value_are_equal_and_have_the_same_hash_code(string[] texts)
    {
        var numbers = texts.Select(JsonNumber.Parse).ToArray();

        foreach (var left in numbers)
        {
            foreach (var right in numbers)
            {
                Assert.Equal(left, right);
                Assert.Equal(left.GetHashCode(), right.GetHashCode());
            }
        }
    }

    [Fact]
    public void Numbers_with_different_values_are_not_equal_and_agree_with_DeepEquals()
    {
        var texts = groups.SelectMany(group => group).Concat(["2", "-1", "1.5", "1e401", "1e-401", "0.10000000000000000000000000000000001"]).ToArray();

        foreach (var left in texts)
        {
            foreach (var right in texts)
            {
                //  DeepEquals throws for an exponent beyond the range of int. JsonNumber has no such limit.
                bool expected;
                try
                {
                    expected = JsonElement.DeepEquals(Element(left), Element(right));
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }

                Assert.True(expected == (JsonNumber.Parse(left) == JsonNumber.Parse(right)), $"{left} and {right}");
            }
        }
    }

    [Fact]
    public void Numbers_from_dotnet_equal_parsed_numbers()
    {
        Assert.Equal(JsonNumber.Parse("1"), JsonNumber.From(1L));
        Assert.Equal(JsonNumber.Parse("1"), JsonNumber.From(1.00m));
        Assert.Equal(JsonNumber.Parse("1"), JsonNumber.From(1.0));
        Assert.Equal(JsonNumber.Parse("0.25"), JsonNumber.From(0.25));
        Assert.Equal(JsonNumber.Parse("1e-5"), JsonNumber.From(0.00001));
        Assert.Equal(JsonNumber.Parse("0"), JsonNumber.From(-0.0));
        Assert.Equal(JsonNumber.Parse("1").GetHashCode(), JsonNumber.From(1.00m).GetHashCode());
        Assert.Equal(JsonNumber.Parse("1e-5").GetHashCode(), JsonNumber.From(0.00001).GetHashCode());
        Assert.NotEqual(JsonNumber.Parse("0.1"), JsonNumber.From(0.1m + 0.0000000000000000000000000001m));
    }

    [Fact]
    public void The_default_number_is_zero()
    {
        Assert.Equal(JsonNumber.Parse("0"), default);
        Assert.Equal(0, default(JsonNumber).Sign);
        Assert.Equal("0", default(JsonNumber).ToString());
    }

    [Theory]
    [InlineData("2", 1, true)]
    [InlineData("2.0", 1, true)]
    [InlineData("1.5e1", 1, true)]
    [InlineData("1.5", 1, false)]
    [InlineData("-1e-400", -1, false)]
    [InlineData("1e400", 1, true)]
    [InlineData("-0.0", 0, true)]
    public void Sign_and_IsInteger_come_from_the_text(string text, int sign, bool isInteger)
    {
        var number = JsonNumber.Parse(text);

        Assert.Equal(sign, number.Sign);
        Assert.Equal(isInteger, number.IsInteger);
    }

    [Theory]
    [InlineData("2.0", true, 2L)]
    [InlineData("1e2", true, 100L)]
    [InlineData("9223372036854775807", true, long.MaxValue)]
    [InlineData("-9223372036854775808", true, long.MinValue)]
    [InlineData("9223372036854775808", false, 0L)]
    [InlineData("-9223372036854775809", false, 0L)]
    [InlineData("1.5", false, 0L)]
    public void TryGetInt64_accepts_any_integer_form_in_range(string text, bool expected, long value)
    {
        Assert.Equal(expected, JsonNumber.Parse(text).TryGetInt64(out var actual));
        Assert.Equal(value, actual);
    }

    [Fact]
    public void Conversions_of_numbers_from_dotnet_give_their_values()
    {
        JsonNumber portions = 3L;
        JsonNumber price = 12.50m;
        var weight = JsonNumber.From(0.25);

        Assert.True(portions.IsInteger);
        Assert.True(JsonNumber.From(2.00m).IsInteger);
        Assert.False(price.IsInteger);
        Assert.True(JsonNumber.From(2.0).IsInteger);
        Assert.False(weight.IsInteger);

        Assert.True(portions.TryGetDecimal(out var portionsDecimal));
        Assert.Equal(3m, portionsDecimal);
        Assert.True(price.TryGetDecimal(out var priceDecimal));
        Assert.Equal(12.50m, priceDecimal);
        Assert.True(portions.TryGetDouble(out var portionsDouble));
        Assert.Equal(3.0, portionsDouble);
        Assert.True(weight.TryGetDouble(out var weightDouble));
        Assert.Equal(0.25, weightDouble);
        Assert.True(portions.TryGetInt64(out var portionsInt64));
        Assert.Equal(3L, portionsInt64);
        Assert.True(JsonNumber.From(4.0).TryGetInt64(out var fromDouble));
        Assert.Equal(4L, fromDouble);
        Assert.False(price.TryGetInt64(out _));
    }

    [Fact]
    public void Equality_operators_compare_values()
    {
        Assert.True(JsonNumber.From(1L) == JsonNumber.Parse("1.0"));
        Assert.False(JsonNumber.From(1L) != JsonNumber.From(1.0m));
        Assert.True(JsonNumber.From(1L) != JsonNumber.From(2L));
        Assert.True(JsonNumber.From(1L).Equals((object)JsonNumber.From(1.0)));
        Assert.False(JsonNumber.From(1L).Equals((object)1L));
    }

    [Fact]
    public void Conversions_report_range_errors()
    {
        Assert.True(JsonNumber.Parse("1.50").TryGetDecimal(out var exact));
        Assert.Equal(1.50m, exact);
        Assert.False(JsonNumber.Parse("1e400").TryGetDecimal(out _));
        Assert.False(JsonNumber.Parse("1e400").TryGetDouble(out _));
        Assert.True(JsonNumber.Parse("1e-400").TryGetDouble(out var tiny));
        Assert.Equal(0, tiny);
        Assert.True(JsonNumber.From(0.1).TryGetDecimal(out var fromDouble));
        Assert.Equal(0.1m, fromDouble);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("01")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("+1")]
    [InlineData("1 2")]
    [InlineData("NaN")]
    [InlineData("\"1\"")]
    public void TryParse_rejects_text_that_is_not_one_JSON_number(string text)
    {
        Assert.False(JsonNumber.TryParse(text, out _));
    }

    [Fact]
    public void From_double_rejects_values_that_JSON_cannot_hold()
    {
        Assert.Throws<ArgumentException>(() => JsonNumber.From(double.NaN));
        Assert.Throws<ArgumentException>(() => JsonNumber.From(double.PositiveInfinity));
    }

    [Fact]
    public void Number_keywords_hold_JsonNumbers_from_the_reader_and_the_builder()
    {
        var parsed = JsonSchema.Parse("""{"minimum":-1.50,"maximum":1e400}""");
        JsonSchema built = new JsonSchemaBuilder().Minimum(-1.50m).Maximum(JsonNumber.Parse("1e400")).MultipleOf(JsonNumber.From(0.25));

        Assert.Equal(parsed.GetKeyword<MinimumKeyword>()!.Value, built.GetKeyword<MinimumKeyword>()!.Value);
        Assert.Equal(parsed.GetKeyword<MaximumKeyword>()!.Value, built.GetKeyword<MaximumKeyword>()!.Value);
        Assert.Equal("""{"minimum":-1.50,"maximum":1e400,"multipleOf":0.25}""", built.ToJsonString());
        Assert.Equal("1e400", JsonNumber.Parse("1e400").ToJsonElement().GetRawText());
        Assert.Equal("0.25", JsonNumber.From(0.25).ToJsonElement().GetRawText());
    }

    [Fact]
    public void MultipleOf_rejects_values_that_are_not_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultipleOfKeyword(JsonNumber.Parse("-1e-400")));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultipleOfKeyword(default));
        Assert.Equal(1, new MultipleOfKeyword(JsonNumber.Parse("1e-400")).Value.Sign);
    }

    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement;
}
