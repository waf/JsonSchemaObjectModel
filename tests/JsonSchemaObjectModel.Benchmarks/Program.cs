using BenchmarkDotNet.Running;

//  With no arguments, run every benchmark. Pass BenchmarkDotNet filters to scope a run, for example:
//  dotnet run -c Release --project tests/JsonSchemaObjectModel.Benchmarks -- --filter *Parse*
var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
if (args.Length == 0)
{
    switcher.RunAll();
}
else
{
    switcher.Run(args);
}
