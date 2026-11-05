using Json.Schema;
using JsonEverything = Json.Schema;
using Ours = JsonSchemaObjectModel;

namespace JsonSchemaObjectModel.Benchmarks;

/// <summary>
/// Builds the same restaurant-order schema with each library.
/// </summary>
/// <remarks>
/// - The schema describes an order form: a customer, then one <c>prefixItems</c> entry per order line.
/// - Each line uses <c>const</c>, <c>pattern</c>, <c>format</c> and <c>required</c>, and an <c>enum</c> per modifier group.
/// - The schema is different for each order, so it is built at run time.
/// </remarks>
public static class OrderSchemas
{
    private static readonly string[] modifierOptions = ["NONE", "CHEESE", "BACON", "AVOCADO", "EGG"];

    private static Guid MenuItemId(int line) => new(line, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]);

    public static Ours.JsonSchema BuildOurs(int lineCount)
    {
        var lines = new List<Ours.JsonSchema>(lineCount);
        for (var line = 0; line < lineCount; line++)
        {
            var modifiers = new List<Ours.JsonSchema>(2);
            for (var group = 0; group < 2; group++)
            {
                modifiers.Add(
                    new Ours.JsonSchemaBuilder()
                        .Properties(
                            ("modifierGroupKey", new Ours.JsonSchemaBuilder().Const($"GROUP-{group}")),
                            ("selectedOptionKey", new Ours.JsonSchemaBuilder().Enum(modifierOptions))
                        )
                        .Required("modifierGroupKey", "selectedOptionKey")
                );
            }

            lines.Add(
                new Ours.JsonSchemaBuilder()
                    .Properties(
                        ("menuItemId", new Ours.JsonSchemaBuilder().Const(MenuItemId(line))),
                        ("itemCode", new Ours.JsonSchemaBuilder().Const($"M{line}")),
                        ("course", new Ours.JsonSchemaBuilder().Const("MAIN")),
                        ("name", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String)),
                        ("notes", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String)),
                        ("size", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String).Pattern("^[SML]$")),
                        ("pickupDate", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String).Format(Ours.Formats.Date)),
                        ("allergenCode", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String)),
                        ("preparedBy", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String).Format(Ours.Formats.Date)),
                        ("receiptEmail", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String).Format(Ours.Formats.Email)),
                        (
                            "modifiers",
                            new Ours.JsonSchemaBuilder()
                                .Type(Ours.SchemaValueType.Array)
                                .PrefixItems(modifiers)
                                .MinItems(2)
                                .MaxItems(2)
                        )
                    )
                    .Required("name", "notes", "size", "pickupDate", "allergenCode", "preparedBy", "modifiers")
            );
        }

        return new Ours.JsonSchemaBuilder()
            .Properties(
                (
                    "customer",
                    new Ours.JsonSchemaBuilder()
                        .Type(Ours.SchemaValueType.Object)
                        .Properties(
                            ("firstName", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String)),
                            ("lastName", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String)),
                            ("email", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String).Format(Ours.Formats.Email)),
                            ("phoneNumber", new Ours.JsonSchemaBuilder().Type(Ours.SchemaValueType.String))
                        )
                        .Required("lastName", "email", "phoneNumber")
                ),
                (
                    "lines",
                    new Ours.JsonSchemaBuilder()
                        .Type(Ours.SchemaValueType.Array)
                        .PrefixItems(lines)
                        .MinItems((ulong)lineCount)
                        .MaxItems((ulong)lineCount)
                )
            )
            .Required("customer", "lines")
            .Build();
    }

    /// <summary>
    /// Build options with a registry of their own. JsonSchema.Net otherwise registers each schema globally.
    /// </summary>
    public static BuildOptions OwnRegistry() => new() { SchemaRegistry = new SchemaRegistry() };

    public static JsonEverything.JsonSchema BuildJsonEverything(int lineCount)
    {
        var lines = new List<JsonEverything.JsonSchemaBuilder>(lineCount);
        for (var line = 0; line < lineCount; line++)
        {
            var modifiers = new List<JsonEverything.JsonSchemaBuilder>(2);
            for (var group = 0; group < 2; group++)
            {
                modifiers.Add(
                    new JsonEverything.JsonSchemaBuilder()
                        .Properties(
                            ("modifierGroupKey", new JsonEverything.JsonSchemaBuilder().Const($"GROUP-{group}")),
                            ("selectedOptionKey", new JsonEverything.JsonSchemaBuilder().Enum(modifierOptions))
                        )
                        .Required("modifierGroupKey", "selectedOptionKey")
                );
            }

            lines.Add(
                new JsonEverything.JsonSchemaBuilder()
                    .Properties(
                        ("menuItemId", new JsonEverything.JsonSchemaBuilder().Const(MenuItemId(line))),
                        ("itemCode", new JsonEverything.JsonSchemaBuilder().Const($"M{line}")),
                        ("course", new JsonEverything.JsonSchemaBuilder().Const("MAIN")),
                        ("name", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String)),
                        ("notes", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String)),
                        ("size", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String).Pattern("^[SML]$")),
                        ("pickupDate", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String).Format(JsonEverything.Formats.Date)),
                        ("allergenCode", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String)),
                        ("preparedBy", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String).Format(JsonEverything.Formats.Date)),
                        ("receiptEmail", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String).Format(JsonEverything.Formats.Email)),
                        (
                            "modifiers",
                            new JsonEverything.JsonSchemaBuilder()
                                .Type(JsonEverything.SchemaValueType.Array)
                                .PrefixItems(modifiers)
                                .MinItems(2)
                                .MaxItems(2)
                        )
                    )
                    .Required("name", "notes", "size", "pickupDate", "allergenCode", "preparedBy", "modifiers")
            );
        }

        return new JsonEverything.JsonSchemaBuilder()
            .Properties(
                (
                    "customer",
                    new JsonEverything.JsonSchemaBuilder()
                        .Type(JsonEverything.SchemaValueType.Object)
                        .Properties(
                            ("firstName", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String)),
                            ("lastName", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String)),
                            ("email", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String).Format(JsonEverything.Formats.Email)),
                            ("phoneNumber", new JsonEverything.JsonSchemaBuilder().Type(JsonEverything.SchemaValueType.String))
                        )
                        .Required("lastName", "email", "phoneNumber")
                ),
                (
                    "lines",
                    new JsonEverything.JsonSchemaBuilder()
                        .Type(JsonEverything.SchemaValueType.Array)
                        .PrefixItems(lines)
                        .MinItems((uint)lineCount)
                        .MaxItems((uint)lineCount)
                )
            )
            .Required("customer", "lines")
            .Build(OwnRegistry());
    }
}
