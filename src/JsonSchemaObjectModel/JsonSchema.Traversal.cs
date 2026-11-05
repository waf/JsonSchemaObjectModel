using System.Runtime.InteropServices;

namespace JsonSchemaObjectModel;

public sealed partial class JsonSchema
{
    /// <summary>
    /// Visits this schema and its subschemas, parent before children, in document order.
    /// </summary>
    /// <remarks>
    /// - The walk goes into the subschemas of keywords that implement <see cref="ISingleSubschemaKeyword"/>,
    ///   <see cref="ISubschemaArrayKeyword"/> or <see cref="ISubschemaMapKeyword"/>. It does not resolve <c>$ref</c>.
    /// - The visitor controls the walk with <see cref="WalkAction"/>.
    /// - With a struct visitor, the walk allocates nothing. The visitor is passed by reference, so it can keep a result.
    /// </remarks>
    public void Walk<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, ISchemaVisitor
    {
        var path = LocationStack.Create();
        try
        {
            Walk(this, ref visitor, ref path);
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>
    /// Visits this schema and its subschemas, parent before children, in document order.
    /// </summary>
    /// <remarks>See <see cref="Walk{TVisitor}(ref TVisitor)"/>. Use that overload with a struct to avoid a delegate.</remarks>
    public void Walk(SchemaVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        var adapter = new DelegateVisitor(visitor);
        Walk(ref adapter);
    }

    /// <summary>
    /// Returns a copy of this schema in which the rewriter has replaced schemas. This schema does not change.
    /// </summary>
    /// <remarks>
    /// - The rewrite goes children first. The rewriter gets each schema after its subschemas are rewritten.
    ///   It does not get its own result again.
    /// - Unchanged parts are shared. If the rewriter returns each schema that it gets, the result is this instance.
    ///   A change allocates only on the path from the changed schema to the root.
    /// - The rewrite goes into the same keywords as <see cref="Walk{TVisitor}(ref TVisitor)"/>, and uses their
    ///   <c>WithSchema</c> or <c>WithSchemas</c> methods to rebuild them.
    /// - With a struct rewriter, a rewrite that changes nothing allocates nothing.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The rewriter returned <see langword="null"/>.</exception>
    public JsonSchema Rewrite<TRewriter>(ref TRewriter rewriter)
        where TRewriter : struct, ISchemaRewriter
    {
        var path = LocationStack.Create();
        try
        {
            return Rewrite(this, ref rewriter, ref path);
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>
    /// Returns a copy of this schema in which the rewriter has replaced schemas. This schema does not change.
    /// </summary>
    /// <remarks>See <see cref="Rewrite{TRewriter}(ref TRewriter)"/>. Use that overload with a struct to avoid a delegate.</remarks>
    /// <exception cref="InvalidOperationException">The rewriter returned <see langword="null"/>.</exception>
    public JsonSchema Rewrite(SchemaRewriter rewrite)
    {
        ArgumentNullException.ThrowIfNull(rewrite);
        var adapter = new DelegateRewriter(rewrite);
        return Rewrite(ref adapter);
    }

    //  Returns false if the visitor stopped the walk.
    private static bool Walk<TVisitor>(JsonSchema schema, ref TVisitor visitor, ref LocationStack path)
        where TVisitor : struct, ISchemaVisitor
    {
        switch (visitor.Visit(schema, path.Location))
        {
            case WalkAction.Stop:
                return false;
            case WalkAction.SkipChildren:
                return true;
        }

        foreach (var keyword in schema.keywords)
        {
            switch (keyword)
            {
                case ISingleSubschemaKeyword single:
                    path.Push(keyword.Name, null, -1);
                    var continues = Walk(single.Schema, ref visitor, ref path);
                    path.Pop();
                    if (!continues)
                    {
                        return false;
                    }

                    break;
                case ISubschemaArrayKeyword array:
                    var schemas = array.Schemas;
                    for (var index = 0; index < schemas.Length; index++)
                    {
                        path.Push(keyword.Name, null, index);
                        continues = Walk(schemas[index], ref visitor, ref path);
                        path.Pop();
                        if (!continues)
                        {
                            return false;
                        }
                    }

                    break;
                case ISubschemaMapKeyword map:
                    foreach (var (name, child) in map.Schemas.Entries)
                    {
                        path.Push(keyword.Name, name, -1);
                        continues = Walk(child, ref visitor, ref path);
                        path.Pop();
                        if (!continues)
                        {
                            return false;
                        }
                    }

                    break;
            }
        }

        return true;
    }

    private static JsonSchema Rewrite<TRewriter>(JsonSchema schema, ref TRewriter rewriter, ref LocationStack path)
        where TRewriter : struct, ISchemaRewriter
    {
        //  Copy the keyword array only when the first keyword changes.
        IJsonSchemaKeyword[]? changed = null;
        var keywords = schema.keywords;
        for (var position = 0; position < keywords.Length; position++)
        {
            var keyword = keywords[position];
            var rewritten = RewriteKeyword(keyword, ref rewriter, ref path);
            if (!ReferenceEquals(rewritten, keyword))
            {
                changed ??= [.. keywords];
                changed[position] = rewritten;
            }
        }

        var current = changed is null ? schema : new JsonSchema(ImmutableCollectionsMarshal.AsImmutableArray(changed));
        return rewriter.Rewrite(current, path.Location)
            ?? throw new InvalidOperationException($"The rewriter returned null for the schema at '{path.Location.ToPointer()}'.");
    }

    private static IJsonSchemaKeyword RewriteKeyword<TRewriter>(IJsonSchemaKeyword keyword, ref TRewriter rewriter, ref LocationStack path)
        where TRewriter : struct, ISchemaRewriter
    {
        switch (keyword)
        {
            case ISingleSubschemaKeyword single:
                path.Push(keyword.Name, null, -1);
                var child = Rewrite(single.Schema, ref rewriter, ref path);
                path.Pop();
                return ReferenceEquals(child, single.Schema) ? keyword : single.WithSchema(child);
            case ISubschemaArrayKeyword array:
                var schemas = array.Schemas;
                JsonSchema[]? changedSchemas = null;
                for (var index = 0; index < schemas.Length; index++)
                {
                    path.Push(keyword.Name, null, index);
                    var rewritten = Rewrite(schemas[index], ref rewriter, ref path);
                    path.Pop();
                    if (!ReferenceEquals(rewritten, schemas[index]))
                    {
                        changedSchemas ??= [.. schemas];
                        changedSchemas[index] = rewritten;
                    }
                }

                return changedSchemas is null ? keyword : array.WithSchemas(ImmutableCollectionsMarshal.AsImmutableArray(changedSchemas));
            case ISubschemaMapKeyword map:
                var entries = map.Schemas.Entries;
                KeyValuePair<string, JsonSchema>[]? changedEntries = null;
                for (var position = 0; position < entries.Length; position++)
                {
                    var (name, original) = entries[position];
                    path.Push(keyword.Name, name, -1);
                    var rewritten = Rewrite(original, ref rewriter, ref path);
                    path.Pop();
                    if (!ReferenceEquals(rewritten, original))
                    {
                        changedEntries ??= entries.ToArray();
                        changedEntries[position] = new(name, rewritten);
                    }
                }

                if (changedEntries is null)
                {
                    return keyword;
                }

                //  The names did not change, so they are still unique. The new map adopts the array without a copy.
                SchemaMap.TryCreate(changedEntries, out var changedMap, out _);
                return map.WithSchemas(changedMap!);
            default:
                return keyword;
        }
    }

    private readonly struct DelegateVisitor(SchemaVisitor visitor) : ISchemaVisitor
    {
        public WalkAction Visit(JsonSchema schema, in SchemaLocation location) => visitor(schema, location);
    }

    private readonly struct DelegateRewriter(SchemaRewriter rewrite) : ISchemaRewriter
    {
        public JsonSchema Rewrite(JsonSchema schema, in SchemaLocation location) => rewrite(schema, location);
    }
}
