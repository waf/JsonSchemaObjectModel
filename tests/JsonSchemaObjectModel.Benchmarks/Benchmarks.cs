using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Json.Pointer;
using JsonEverything = Json.Schema;
using NJsonSchemaModel = NJsonSchema.JsonSchema;
using Ours = JsonSchemaObjectModel;

namespace JsonSchemaObjectModel.Benchmarks;

/// <summary>
/// The operations a service does with a schema object model.
/// </summary>
/// <remarks>
/// - Build and Write: an ordering API builds a schema for each order and sends it in the response.
/// - Parse and Navigate: the order form reads the schema back and looks up the fields of each order line.
/// - NJsonSchema models draft 4 to 7. It keeps 2020-12 keywords such as <c>prefixItems</c> as extension data,
///   so it takes part in Parse and Write only.
/// - JsonSchema.Net registers every schema it builds in <see cref="JsonEverything.SchemaRegistry.Global"/> by default.
///   A schema for each order then grows that registry without limit, so these benchmarks give each build its own
///   registry. That is what a long-running service must do.
/// - Run: <c>dotnet run -c Release --project tests/JsonSchemaObjectModel.Benchmarks -- --filter *</c>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SchemaObjectModelBenchmarks
{
    private const string JsonSchemaNetName = "JsonSchema.Net 9.6.0";
    private const string NJsonSchemaName = "NJsonSchema 11.6.1";

    private string json = "";
    private Ours.JsonSchema ours = null!;
    private JsonEverything.JsonSchema jsonEverything = null!;
    private NJsonSchemaModel nJsonSchema = null!;
    private string lastLinePointer = "";

    [Params(1, 9)]
    public int LineCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        ours = OrderSchemas.BuildOurs(LineCount);
        json = ours.ToJsonString();
        jsonEverything = ParseJsonEverything();
        nJsonSchema = ParseNJsonSchema();
        lastLinePointer = $"/properties/lines/prefixItems/{LineCount - 1}/properties/itemCode";

        //  All libraries must agree on the input, or the comparison means nothing.
        if (!JsonElement.DeepEquals(Parse(json), Parse(JsonSerializer.Serialize(BuildJsonEverything()))))
        {
            throw new InvalidOperationException("The two builders do not produce the same schema.");
        }

        if (!JsonElement.DeepEquals(Parse(json), Parse(WriteJsonEverything())))
        {
            throw new InvalidOperationException($"{JsonSchemaNetName} does not write the schema it parsed.");
        }

        if (NavigateOurs() != $"\"M{LineCount - 1}\"" || NavigateJsonEverything() != NavigateOurs())
        {
            throw new InvalidOperationException("Navigation does not find the same keyword in each library.");
        }

        static JsonElement Parse(string text) => JsonDocument.Parse(text).RootElement;
    }

    [Benchmark(Baseline = true, Description = "JsonSchemaObjectModel"), BenchmarkCategory("Build")]
    public Ours.JsonSchema BuildOurs() => OrderSchemas.BuildOurs(LineCount);

    [Benchmark(Description = JsonSchemaNetName), BenchmarkCategory("Build")]
    public JsonEverything.JsonSchema BuildJsonEverything() => OrderSchemas.BuildJsonEverything(LineCount);

    [Benchmark(Baseline = true, Description = "JsonSchemaObjectModel"), BenchmarkCategory("Write")]
    public string WriteOurs() => ours.ToJsonString();

    [Benchmark(Description = JsonSchemaNetName), BenchmarkCategory("Write")]
    public string WriteJsonEverything() => JsonSerializer.Serialize(jsonEverything);

    [Benchmark(Description = NJsonSchemaName), BenchmarkCategory("Write")]
    public string WriteNJsonSchema() => nJsonSchema.ToJson();

    [Benchmark(Baseline = true, Description = "JsonSchemaObjectModel"), BenchmarkCategory("Parse")]
    public Ours.JsonSchema ParseOurs() => Ours.JsonSchema.Parse(json);

    [Benchmark(Description = JsonSchemaNetName), BenchmarkCategory("Parse")]
    public JsonEverything.JsonSchema ParseJsonEverything() => JsonEverything.JsonSchema.FromText(json, OrderSchemas.OwnRegistry());

    [Benchmark(Description = NJsonSchemaName), BenchmarkCategory("Parse")]
    public NJsonSchemaModel ParseNJsonSchema() => NJsonSchemaModel.FromJsonAsync(json).GetAwaiter().GetResult();

    [Benchmark(Baseline = true, Description = "JsonSchemaObjectModel"), BenchmarkCategory("Navigate")]
    public string? NavigateOurs() => ours.FindSubschema(lastLinePointer)?.GetKeyword<Keywords.ConstKeyword>()?.Value.GetRawText();

    [Benchmark(Description = JsonSchemaNetName), BenchmarkCategory("Navigate")]
    public string? NavigateJsonEverything() =>
        jsonEverything
            .FindSubschema(JsonPointer.Parse(lastLinePointer), default)
            ?.Keywords.FirstOrDefault(keyword => keyword.Handler.Name == "const")
            ?.RawValue.GetRawText();
}
