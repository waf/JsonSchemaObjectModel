using System.Text.Json;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// Loads the schemas of the official JSON Schema Test Suite (draft 2020-12).
/// </summary>
/// <remarks>
/// The suite is a git submodule at <c>tests/JSON-Schema-Test-Suite</c>.
/// Each case is one schema from one test group.
/// </remarks>
public static class TestSuite
{
    private static readonly string root = Path.Combine(AppContext.BaseDirectory, "TestSuite", "draft2020-12");

    public static TheoryData<string, int> SchemaCases()
    {
        var data = new TheoryData<string, int>();
        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var groupCount = LoadGroups(relative).GetArrayLength();
            for (var group = 0; group < groupCount; group++)
            {
                data.Add(relative, group);
            }
        }

        return data;
    }

    public static JsonElement LoadSchema(string relativePath, int group) =>
        LoadGroups(relativePath)[group].GetProperty("schema").Clone();

    public static string LoadDescription(string relativePath, int group) =>
        LoadGroups(relativePath)[group].GetProperty("description").GetString()!;

    private static JsonElement LoadGroups(string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, relativePath)));
        return document.RootElement.Clone();
    }
}
