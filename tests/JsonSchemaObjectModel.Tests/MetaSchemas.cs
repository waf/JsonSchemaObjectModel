using LateApexEarlySpeed.Json.Schema;
using LateApexEarlySpeed.Json.Schema.Common;

namespace JsonSchemaObjectModel.Tests;

/// <summary>
/// Checks schema documents against meta-schemas with an independent validator (LateApexEarlySpeed.Json.Schema).
/// </summary>
/// <remarks>
/// The official draft 2020-12 meta-schemas are in <c>TestData/MetaSchemas</c>, so the tests do not use the network.
/// </remarks>
public static class MetaSchemas
{
    private static readonly string directory = Path.Combine(AppContext.BaseDirectory, "TestData", "MetaSchemas", "draft2020-12");

    /// <summary>
    /// Validates <paramref name="schemaJson"/> against <paramref name="metaSchemaJson"/>.
    /// The validator can resolve the draft 2020-12 meta-schemas and any <paramref name="extraDocuments"/>.
    /// </summary>
    public static ValidationResult Validate(string schemaJson, string metaSchemaJson, params string[] extraDocuments)
    {
        var validator = new JsonValidator(metaSchemaJson);
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Where(file => Path.GetFileName(file) != "schema.json"))
        {
            validator.AddExternalDocument(File.ReadAllText(file));
        }

        if (metaSchemaJson != Draft202012)
        {
            validator.AddExternalDocument(Draft202012);
        }

        foreach (var document in extraDocuments)
        {
            validator.AddExternalDocument(document);
        }

        return validator.Validate(schemaJson, new JsonSchemaOptions { OutputFormat = OutputFormat.List });
    }

    /// <summary>
    /// Validates <paramref name="schemaJson"/> against the draft 2020-12 meta-schema.
    /// </summary>
    public static ValidationResult ValidateDraft202012(string schemaJson) => Validate(schemaJson, Draft202012);

    public static string Draft202012 { get; } = File.ReadAllText(Path.Combine(directory, "schema.json"));

    public static string Describe(ValidationResult result) =>
        string.Join(Environment.NewLine, result.ValidationErrors.Select(error => $"{error.InstanceLocation}: {error.ErrorMessage}"));
}
