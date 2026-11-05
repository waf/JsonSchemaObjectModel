using System.Buffers;
using System.Globalization;
using System.Text;

namespace JsonSchemaObjectModel;

/// <summary>
/// What a <see cref="ISchemaVisitor"/> tells the walk to do next.
/// </summary>
public enum WalkAction
{
    /// <summary>Visit the subschemas of this schema, then continue.</summary>
    Continue,

    /// <summary>Do not visit the subschemas of this schema. Continue with the next schema.</summary>
    SkipChildren,

    /// <summary>Stop the walk.</summary>
    Stop,
}

/// <summary>
/// Visits the schemas of a walk. See <see cref="JsonSchema.Walk{TVisitor}(ref TVisitor)"/>.
/// </summary>
/// <remarks>
/// Implement it on a struct. The walk then calls it without a delegate or an allocation.
/// </remarks>
public interface ISchemaVisitor
{
    /// <summary>
    /// Visits one schema.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <param name="location">The location of the schema in the root. It is valid only during this call.</param>
    WalkAction Visit(JsonSchema schema, in SchemaLocation location);
}

/// <summary>
/// Visits one schema of a walk. See <see cref="JsonSchema.Walk(SchemaVisitor)"/>.
/// </summary>
/// <param name="schema">The schema.</param>
/// <param name="location">The location of the schema in the root. It is valid only during this call.</param>
public delegate WalkAction SchemaVisitor(JsonSchema schema, in SchemaLocation location);

/// <summary>
/// Rewrites the schemas of a rewrite. See <see cref="JsonSchema.Rewrite{TRewriter}(ref TRewriter)"/>.
/// </summary>
/// <remarks>
/// Implement it on a struct. The rewrite then calls it without a delegate or an allocation.
/// </remarks>
public interface ISchemaRewriter
{
    /// <summary>
    /// Rewrites one schema. Its subschemas are already rewritten.
    /// </summary>
    /// <param name="schema">The schema, with rewritten subschemas.</param>
    /// <param name="location">The location of the schema in the original root. It is valid only during this call.</param>
    /// <returns>The new schema, or <paramref name="schema"/> to keep it.</returns>
    JsonSchema Rewrite(JsonSchema schema, in SchemaLocation location);
}

/// <summary>
/// Rewrites one schema of a rewrite. See <see cref="JsonSchema.Rewrite(SchemaRewriter)"/>.
/// </summary>
/// <param name="schema">The schema, with rewritten subschemas.</param>
/// <param name="location">The location of the schema in the original root. It is valid only during this call.</param>
/// <returns>The new schema, or <paramref name="schema"/> to keep it.</returns>
public delegate JsonSchema SchemaRewriter(JsonSchema schema, in SchemaLocation location);

/// <summary>
/// One step from a schema into a subschema: the keyword, and the property name or index in its value.
/// </summary>
/// <param name="KeywordName">The keyword that holds the subschema, for example <c>properties</c>.</param>
/// <param name="PropertyName">The name in a map keyword such as <c>properties</c>, or <see langword="null"/>.</param>
/// <param name="Index">The index in an array keyword such as <c>allOf</c>, or -1.</param>
public readonly record struct SchemaLocationSegment(string KeywordName, string? PropertyName, int Index);

/// <summary>
/// The location of a subschema in the root schema of a walk or a rewrite.
/// </summary>
/// <remarks>
/// - The location is valid only during the call that gets it. Copy what you need, for example with <see cref="ToPointer"/>.
/// - It costs nothing until you read it. <see cref="ToPointer"/> allocates the pointer string.
/// </remarks>
public readonly ref struct SchemaLocation
{
    internal SchemaLocation(ReadOnlySpan<SchemaLocationSegment> segments) => Segments = segments;

    /// <summary>
    /// The steps from the root to the schema. The root has no steps.
    /// </summary>
    public ReadOnlySpan<SchemaLocationSegment> Segments { get; }

    /// <summary>
    /// The number of keywords between the root and the schema. The root has the depth 0.
    /// </summary>
    public int Depth => Segments.Length;

    /// <summary>
    /// <see langword="true"/> for the root schema.
    /// </summary>
    public bool IsRoot => Segments.IsEmpty;

    /// <summary>
    /// The keyword that holds the schema, for example <c>items</c>, or <see langword="null"/> for the root.
    /// </summary>
    public string? KeywordName => IsRoot ? null : Segments[^1].KeywordName;

    /// <summary>
    /// The name of the schema in a map keyword such as <c>properties</c>, or <see langword="null"/>.
    /// </summary>
    public string? PropertyName => IsRoot ? null : Segments[^1].PropertyName;

    /// <summary>
    /// The index of the schema in an array keyword such as <c>allOf</c>, or <see langword="null"/>.
    /// </summary>
    public int? Index => IsRoot || Segments[^1].Index < 0 ? null : Segments[^1].Index;

    /// <summary>
    /// Returns the RFC 6901 JSON Pointer of the schema, for example <c>/properties/menus/items</c>.
    /// The root has the empty pointer. <see cref="JsonSchema.FindSubschema(string)"/> accepts the result.
    /// </summary>
    public string ToPointer()
    {
        if (IsRoot)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var (keywordName, propertyName, index) in Segments)
        {
            AppendToken(builder, keywordName);
            if (propertyName is not null)
            {
                AppendToken(builder, propertyName);
            }
            else if (index >= 0)
            {
                builder.Append('/').Append(index.ToString(CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns the JSON Pointer of the schema. See <see cref="ToPointer"/>.
    /// </summary>
    public override string ToString() => ToPointer();

    private static void AppendToken(StringBuilder builder, string token)
    {
        builder.Append('/');
        foreach (var character in token)
        {
            _ = character switch
            {
                '~' => builder.Append("~0"),
                '/' => builder.Append("~1"),
                _ => builder.Append(character),
            };
        }
    }
}

/// <summary>
/// The path of a walk or a rewrite. It uses a pooled array, so a walk allocates nothing after the first use.
/// </summary>
internal struct LocationStack : IDisposable
{
    private SchemaLocationSegment[] segments;
    private int count;

    public static LocationStack Create() => new() { segments = ArrayPool<SchemaLocationSegment>.Shared.Rent(16) };

    public readonly SchemaLocation Location => new(segments.AsSpan(0, count));

    public void Push(string keywordName, string? propertyName, int index)
    {
        if (count == segments.Length)
        {
            var larger = ArrayPool<SchemaLocationSegment>.Shared.Rent(segments.Length * 2);
            segments.AsSpan(0, count).CopyTo(larger);
            ArrayPool<SchemaLocationSegment>.Shared.Return(segments, clearArray: true);
            segments = larger;
        }

        segments[count++] = new(keywordName, propertyName, index);
    }

    public void Pop() => count--;

    public void Dispose()
    {
        if (segments is not null)
        {
            //  Clear the array, so that the pool does not keep references to keyword and property names.
            ArrayPool<SchemaLocationSegment>.Shared.Return(segments, clearArray: true);
            segments = null!;
        }
    }
}
