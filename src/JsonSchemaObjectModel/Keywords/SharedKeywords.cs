namespace JsonSchemaObjectModel.Keywords;

/// <summary>
/// Helpers for the shared instances of immutable keywords.
/// </summary>
internal static class SharedKeywords
{
    /// <summary>
    /// Schemas often use small counts and lengths, so the integer keywords share an instance for 0 to this value.
    /// </summary>
    public const int MaxSharedInteger = 16;

    public static T[] CreateIntegerCache<T>(Func<ulong, T> create)
    {
        var cache = new T[MaxSharedInteger + 1];
        for (var value = 0; value < cache.Length; value++)
        {
            cache[value] = create((ulong)value);
        }

        return cache;
    }
}
