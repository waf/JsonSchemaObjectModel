using System.Globalization;
using System.Text.Json;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// Loads the draft 2020-12 schemas of the official JSON Schema Test Suite.
/// </summary>
/// <remarks>
/// - The suite is a git submodule at <c>tests/JSON-Schema-Test-Suite</c>.
/// - The suite keeps the tests for all drafts in <c>validation/tests</c>.
/// - Each file is an object with a <c>tests</c> array of test cases.
/// - A test case can have a <c>compatibility</c> property. It tells which releases the case applies to.
/// - Each data row is one schema from one test case that applies to 2020-12.
/// - The group index is the position of the case in the <c>tests</c> array of its file.
/// </remarks>
public static class TestSuite
{
    private const int Release = 2020;

    private static readonly string root = Path.Combine(AppContext.BaseDirectory, "TestSuite", "validation");

    public static TheoryData<string, int> SchemaCases()
    {
        var data = new TheoryData<string, int>();
        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var group = 0;
            foreach (var testCase in LoadGroups(relative).EnumerateArray())
            {
                if (AppliesTo(testCase, Release))
                {
                    data.Add(relative, group);
                }

                group++;
            }
        }

        if (data.Count == 0)
        {
            throw new InvalidOperationException($"The Test Suite has no 2020-12 cases in '{root}'. Run 'git submodule update --init'.");
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
        return document.RootElement.GetProperty("tests").Clone();
    }

    /// <summary>
    /// Tells if a test case applies to a release.
    /// </summary>
    /// <remarks>
    /// The <c>compatibility</c> value is a comma-separated list of constraints, from smallest to largest:
    /// - <c>N</c> starts a range at release N.
    /// - <c>&lt;=N</c> ends the open range at release N. With no open range, the range starts at the first release.
    /// - <c>=N</c> is release N only.
    /// - A range that has no end includes all later releases.
    /// - A case with no <c>compatibility</c> applies to all releases.
    /// </remarks>
    private static bool AppliesTo(JsonElement testCase, int release)
    {
        if (!testCase.TryGetProperty("compatibility", out var compatibility))
        {
            return true;
        }

        int? start = null;
        foreach (var part in compatibility.GetString()!.Split(','))
        {
            var constraint = part.Trim();
            if (constraint.StartsWith("<=", StringComparison.Ordinal))
            {
                if ((start ?? int.MinValue) <= release && release <= ParseRelease(constraint[2..]))
                {
                    return true;
                }

                start = null;
            }
            else if (constraint.StartsWith('='))
            {
                if (release == ParseRelease(constraint[1..]))
                {
                    return true;
                }
            }
            else
            {
                start = ParseRelease(constraint);
            }
        }

        return start <= release;
    }

    private static int ParseRelease(string text) => int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
}
