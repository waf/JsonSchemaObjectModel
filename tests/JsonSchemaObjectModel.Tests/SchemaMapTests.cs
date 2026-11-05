using JsonSchemaObjectModel.Keywords;

namespace JsonSchemaObjectModel.Tests;

public class SchemaMapTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(40)]
    public void Lookups_by_string_and_span_find_every_entry_in_order(int count)
    {
        var names = Enumerable.Range(0, count).Select(index => $"menu{index}").ToArray();
        var map = SchemaMap.Create(names.Select(name => KeyValuePair.Create(name, (JsonSchema)new JsonSchemaBuilder().Title(name))));

        Assert.Equal(names, map.Keys);
        Assert.Equal(names, map.Values.Select(schema => schema.GetKeyword<TitleKeyword>()!.Value));
        foreach (var name in names)
        {
            Assert.True(map.TryGetValue(name, out var byString));
            Assert.True(map.TryGetValue(name.AsSpan(), out var bySpan));
            Assert.Same(byString, bySpan);
            Assert.Same(byString, map[name]);
        }

        Assert.False(map.TryGetValue("missing", out _));
        Assert.False(map.TryGetValue("missing".AsSpan(), out _));
        Assert.Throws<KeyNotFoundException>(() => map["missing"]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(40)]
    public void Duplicate_names_are_rejected(int count)
    {
        var entries = Enumerable.Range(0, count).Select(index => KeyValuePair.Create($"menu{index}", JsonSchema.True)).ToList();
        entries.Add(KeyValuePair.Create("menu1", JsonSchema.False));

        Assert.Throws<ArgumentException>(() => SchemaMap.Create(entries));
        var exception = Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            JsonSchema.Parse("{\"properties\":{" + string.Join(",", entries.Select(entry => $"\"{entry.Key}\":true")) + "}}")
        );
        Assert.Equal("/properties/menu1", exception.Path);
    }

    [Fact]
    public void A_keyword_shares_a_map_without_a_copy()
    {
        var map = SchemaMap.Create(("lunch", JsonSchema.True), ("dinner", JsonSchema.False));

        Assert.Same(map, new PropertiesKeyword(map).Schemas);
        Assert.Same(map, SchemaMap.Create(map));
    }

    [Fact]
    public void The_struct_enumerator_gives_the_entries_in_order()
    {
        var schema = JsonSchema.Parse("""{"properties":{"lunch":true,"dinner":false,"brunch":{}}}""");

        var names = new List<string>();
        foreach (var (name, _) in schema.Properties!)
        {
            names.Add(name);
        }

        Assert.Equal(["lunch", "dinner", "brunch"], names);
        Assert.Same(SchemaMap.Empty, JsonSchema.Parse("""{"properties":{}}""").Properties);
    }

    [Fact]
    public void Keys_and_values_are_views_that_do_not_allocate()
    {
        var map = JsonSchema.Parse("""{"properties":{"lunch":true,"dinner":false,"brunch":{}}}""").Properties!;
        var nameLength = 0;
        var schemaCount = 0;

        //  Warm up, so the measured loop does not include JIT work.
        Count(map, ref nameLength, ref schemaCount);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Count(map, ref nameLength, ref schemaCount);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(2 * "lunchdinnerbrunch".Length, nameLength);
        Assert.Equal(6, schemaCount);
        Assert.Equal(3, map.Keys.Count);
        Assert.Equal(["lunch", "dinner", "brunch"], map.Keys);

        static void Count(SchemaMap map, ref int nameLength, ref int schemaCount)
        {
            foreach (var name in map.Keys)
            {
                nameLength += name.Length;
            }

            foreach (var _ in map.Values)
            {
                schemaCount++;
            }
        }
    }

    [Fact]
    public void Default_collections_are_empty()
    {
        Assert.Empty(default(SchemaMap.KeyCollection));
        Assert.Empty(default(SchemaMap.ValueCollection));
        var defaultKeyCount = default(SchemaMap.KeyCollection).Count;
        Assert.Equal(0, defaultKeyCount);

        var count = 0;
        foreach (var _ in default(SchemaMap.ValueCollection))
        {
            count++;
        }

        Assert.Equal(0, count);
    }
}

public class SharedKeywordTests
{
    [Fact]
    public void The_reader_and_the_builder_return_shared_keywords_for_common_values()
    {
        var parsed = JsonSchema.Parse(
            """{"additionalProperties":false,"items":true,"minItems":1,"maxLength":16,"uniqueItems":true,"format":"email"}"""
        );
        var built = new JsonSchemaBuilder()
            .AdditionalProperties(false)
            .Items(true)
            .MinItems(1)
            .MaxLength(16)
            .UniqueItems()
            .Format(Formats.Email)
            .Build();

        for (var index = 0; index < parsed.Keywords.Length; index++)
        {
            Assert.Same(parsed.Keywords[index], built.Keywords[index]);
        }
    }

    [Fact]
    public void Values_without_a_shared_keyword_are_still_correct()
    {
        var schema = JsonSchema.Parse("""{"minItems":17,"format":"x-menu-code","additionalProperties":{"type":"string"}}""");

        Assert.Equal(17UL, schema.GetKeyword<MinItemsKeyword>()!.Value);
        Assert.Equal("x-menu-code", schema.Format);
        Assert.NotSame(MinItemsKeyword.For(17), MinItemsKeyword.For(17));
        Assert.Equal("""{"minItems":17,"format":"x-menu-code","additionalProperties":{"type":"string"}}""", schema.ToJsonString());
    }
}

public class TypedKeywordRegistrationTests
{
    [Fact]
    public void Add_T_registers_the_type_by_its_own_name()
    {
        var options = new JsonSchemaReaderOptions(JsonSchemaKeywordRegistry.CreateDraft202012().Add<CuisineKeyword>());

        var schema = JsonSchema.Parse("""{"cuisine":"thai"}""", options);

        var keyword = schema.GetKeyword<CuisineKeyword>()!;
        Assert.Equal("thai", keyword.Cuisine);
        Assert.Equal("cuisine", keyword.Name);
        Assert.Equal("""{"cuisine":"thai"}""", schema.ToJsonString());
    }

    [Fact]
    public void Add_T_rejects_a_second_reader_and_Set_T_replaces_it()
    {
        var registry = JsonSchemaKeywordRegistry.CreateDraft202012().Add<CuisineKeyword>();

        Assert.Throws<ArgumentException>(() => registry.Add<CuisineKeyword>());
        Assert.Throws<ArgumentException>(() => registry.Add(CuisineKeyword.KeywordName, (value, _) => new UnknownKeyword("cuisine", value)));
        registry.Set<CuisineKeyword>();
    }

    private sealed class CuisineKeyword(string cuisine) : IJsonSchemaKeyword<CuisineKeyword>
    {
        public static string KeywordName => "cuisine";

        public string Name => KeywordName;

        public string Cuisine { get; } = cuisine;

        public static CuisineKeyword Read(System.Text.Json.JsonElement value, JsonSchemaReadContext context) =>
            new(context.ReadString(value));

        public void WriteValue(System.Text.Json.Utf8JsonWriter writer) => writer.WriteStringValue(Cuisine);
    }
}
