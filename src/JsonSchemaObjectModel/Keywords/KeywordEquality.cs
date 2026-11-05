namespace JsonSchemaObjectModel.Keywords;

/// <summary>
/// A keyword that compares its value without writing it to JSON.
/// </summary>
/// <remarks>
/// - <see cref="ValueEquals"/> gets a keyword of the same type.
/// - <see cref="GetValueHashCode"/> must give the same result as <see cref="JsonValueEquality.Element"/>
///   for the JSON that the keyword writes. A typed keyword and an <see cref="UnknownKeyword"/> with the same JSON
///   are then equal, with the same hash code.
/// </remarks>
internal interface IKeywordValueEquality
{
    bool ValueEquals(IJsonSchemaKeyword other);

    int GetValueHashCode();
}

/// <summary>
/// Compares keyword values. Schema equality uses it.
/// </summary>
internal static class KeywordEquality
{
    /// <summary>
    /// Returns <see langword="true"/> if two keywords with the same name have equal JSON values.
    /// </summary>
    public static bool ValueEquals(IJsonSchemaKeyword left, IJsonSchemaKeyword right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        //  Keywords of the same type compare their fields. Keywords of different types, for example a typed keyword
        //  and an UnknownKeyword from a parse with another registry, compare their JSON.
        if (left.GetType() == right.GetType() && left is IKeywordValueEquality typed)
        {
            return typed.ValueEquals(right);
        }

        return JsonValueEquality.ElementEquals(JsonValueEquality.KeywordValue(left), JsonValueEquality.KeywordValue(right));
    }

    /// <summary>
    /// Returns the hash code of a keyword value. It is the hash code of the JSON that the keyword writes.
    /// </summary>
    public static int ValueHash(IJsonSchemaKeyword keyword) =>
        keyword is IKeywordValueEquality typed
            ? typed.GetValueHashCode()
            : JsonValueEquality.Element(JsonValueEquality.KeywordValue(keyword));
}
