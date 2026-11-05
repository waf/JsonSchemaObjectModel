# Benchmarks

These benchmarks compare JsonSchemaObjectModel with JsonSchema.Net and NJsonSchema. They measure the four operations
that a service does with a schema object model:

- **Build:** an ordering API builds a schema for an order with the fluent builder.
- **Write:** the API writes that schema as JSON text for its response.
- **Parse:** the order form reads the JSON text back into an object model.
- **Navigate:** the order form finds the `const` of the last order line with the JSON Pointer
  `/properties/lines/prefixItems/{n}/properties/itemCode`.

The schema is a restaurant order. It has a customer, then one `prefixItems` entry for each order line. Each line uses
`const`, `pattern`, `format`, `required` and an `enum` for each modifier group. `LineCount` is 1 or 9 order lines.

## Run

```console
dotnet run -c Release --project tests/JsonSchemaObjectModel.Benchmarks -- --filter *
dotnet run -c Release --project tests/JsonSchemaObjectModel.Benchmarks -- --filter *Parse*
```

A full run takes about 3 minutes. Before the benchmarks start, `GlobalSetup` makes sure that each library writes the
same JSON and finds the same value. If it throws, the comparison is not valid.

## Results

Run on 2026-11-05 at commit `15fce9c`.

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
AMD Ryzen 9 5900HX with Radeon Graphics 3.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.403
  [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3
```

### Summary

The ratios compare each library with JsonSchemaObjectModel. A higher value is slower, or allocates more.

| Operation | Lines | JsonSchemaObjectModel | JsonSchema.Net 9.6.0 | NJsonSchema 11.6.1 |
|---|---:|---|---|---|
| Build | 1 | 2.13 µs, 6,792 B | 38× time, 21× memory | - |
| Build | 9 | 14.6 µs, 46,472 B | 42× time, 22× memory | - |
| Write | 1 | 2.76 µs, 2,544 B | 1.0× time, 1.0× memory | 32× time, 47× memory |
| Write | 9 | 17.9 µs, 17,280 B | 1.0× time, 1.0× memory | 29× time, 40× memory |
| Parse | 1 | 10.8 µs, 9,216 B | 4.4× time, 9.4× memory | 12× time, 21× memory |
| Parse | 9 | 73.2 µs, 62,120 B | 4.2× time, 9.4× memory | 12× time, 21× memory |
| Navigate | 1 | 71 ns, 32 B | 1.3× time, 2.8× memory | - |
| Navigate | 9 | 71 ns, 32 B | 1.3× time, 2.8× memory | - |

- The full run measured 2.99 µs for Write with 1 line. A run of the Write benchmarks only measured 2.76 µs, the same
  as earlier runs, so the summary uses that value. The table below shows the full run without changes.
- The JsonSchema.Net Build times vary a lot between iterations (StdDev about 13-17% of the mean). Use those ratios
  as approximate values.
- Each benchmark uses 3 warm-up and 5 measured iterations, so small differences between runs are noise.

### Full output

| Method                 | Categories | LineCount | Mean          | Error          | StdDev         | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|----------------------- |----------- |---------- |--------------:|---------------:|---------------:|------:|--------:|---------:|--------:|----------:|------------:|
| JsonSchemaObjectModel  | Build      | 1         |   2,131.20 ns |     326.822 ns |      50.576 ns |  1.00 |    0.03 |   0.8087 |  0.0076 |    6792 B |        1.00 |
| 'JsonSchema.Net 9.6.0' | Build      | 1         |  81,056.77 ns |  40,887.609 ns |  10,618.382 ns | 38.05 |    4.65 |  16.6016 |  2.4414 |  142408 B |       20.97 |
|                        |            |           |               |                |                |       |         |          |         |           |             |
| JsonSchemaObjectModel  | Build      | 9         |  14,600.54 ns |   1,514.681 ns |     234.399 ns |  1.00 |    0.02 |   5.5542 |  0.4425 |   46472 B |        1.00 |
| 'JsonSchema.Net 9.6.0' | Build      | 9         | 608,090.53 ns | 684,045.930 ns | 105,856.865 ns | 41.66 |    6.52 | 117.1875 | 39.0625 | 1001248 B |       21.55 |
|                        |            |           |               |                |                |       |         |          |         |           |             |
| JsonSchemaObjectModel  | Navigate   | 1         |      71.05 ns |       1.419 ns |       0.369 ns |  1.00 |    0.01 |   0.0038 |       - |      32 B |        1.00 |
| 'JsonSchema.Net 9.6.0' | Navigate   | 1         |      95.12 ns |       2.365 ns |       0.614 ns |  1.34 |    0.01 |   0.0105 |       - |      88 B |        2.75 |
|                        |            |           |               |                |                |       |         |          |         |           |             |
| JsonSchemaObjectModel  | Navigate   | 9         |      71.17 ns |       2.963 ns |       0.459 ns |  1.00 |    0.01 |   0.0038 |       - |      32 B |        1.00 |
| 'JsonSchema.Net 9.6.0' | Navigate   | 9         |      94.71 ns |      10.127 ns |       2.630 ns |  1.33 |    0.03 |   0.0105 |       - |      88 B |        2.75 |
|                        |            |           |               |                |                |       |         |          |         |           |             |
| JsonSchemaObjectModel  | Parse      | 1         |  10,848.02 ns |     467.479 ns |     121.403 ns |  1.00 |    0.01 |   1.0986 |  0.0153 |    9216 B |        1.00 |
| 'JsonSchema.Net 9.6.0' | Parse      | 1         |  47,300.35 ns |  20,225.672 ns |   5,252.543 ns |  4.36 |    0.44 |  10.2539 |  0.7324 |   86376 B |        9.37 |
| 'NJsonSchema 11.6.1'   | Parse      | 1         | 134,071.23 ns |  16,716.154 ns |   4,341.132 ns | 12.36 |    0.39 |  23.4375 |  4.3945 |  197544 B |       21.43 |
|                        |            |           |               |                |                |       |         |          |         |           |             |
| JsonSchemaObjectModel  | Parse      | 9         |  73,223.64 ns |   1,697.678 ns |     440.882 ns |  1.00 |    0.01 |   7.3242 |  0.7324 |   62120 B |        1.00 |
| 'JsonSchema.Net 9.6.0' | Parse      | 9         | 305,643.95 ns |  30,567.905 ns |   7,938.388 ns |  4.17 |    0.10 |  68.3594 | 21.4844 |  581824 B |        9.37 |
| 'NJsonSchema 11.6.1'   | Parse      | 9         | 877,567.99 ns |  19,541.866 ns |   5,074.961 ns | 11.99 |    0.09 | 158.2031 | 67.3828 | 1330824 B |       21.42 |
|                        |            |           |               |                |                |       |         |          |         |           |             |
| JsonSchemaObjectModel  | Write      | 1         |   2,985.49 ns |     103.600 ns |      26.904 ns |  1.00 |    0.01 |   0.3014 |       - |    2544 B |        1.00 |
| 'JsonSchema.Net 9.6.0' | Write      | 1         |   2,807.77 ns |      26.826 ns |       6.967 ns |  0.94 |    0.01 |   0.3052 |       - |    2584 B |        1.02 |
| 'NJsonSchema 11.6.1'   | Write      | 1         |  89,502.27 ns |   2,268.854 ns |     351.108 ns | 29.98 |    0.27 |  14.1602 |  0.1221 |  119104 B |       46.82 |
|                        |            |           |               |                |                |       |         |          |         |           |             |
| JsonSchemaObjectModel  | Write      | 9         |  17,878.58 ns |     429.042 ns |      66.395 ns |  1.00 |    0.00 |   2.0447 |       - |   17280 B |        1.00 |
| 'JsonSchema.Net 9.6.0' | Write      | 9         |  17,794.93 ns |     612.206 ns |     158.988 ns |  1.00 |    0.01 |   2.0447 |       - |   17320 B |        1.00 |
| 'NJsonSchema 11.6.1'   | Write      | 9         | 518,281.64 ns |  10,743.000 ns |   2,789.923 ns | 28.99 |    0.17 |  82.0313 |  4.8828 |  691576 B |       40.02 |

## Notes on the comparison

- **NJsonSchema** models drafts 4 to 7. It keeps 2020-12 keywords such as `prefixItems` as extension data, so it is
  in the Parse and Write benchmarks only.
- **JsonSchema.Net** puts each schema that it builds into `SchemaRegistry.Global` by default. With one schema for
  each order, that registry grows without limit. So the benchmarks give each build and parse its own registry, as a
  long-running service must do.
- **JsonSchema.Net 9.x** binaries are under the json-everything maintenance-fee EULA. This project uses them only in
  these benchmarks. The JsonSchemaObjectModel package does not depend on them.
- The results come from one machine. Run the benchmarks again on your hardware before you use the numbers for a
  decision.
