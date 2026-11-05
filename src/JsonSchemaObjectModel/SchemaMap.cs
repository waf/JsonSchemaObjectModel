using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace JsonSchemaObjectModel;

/// <summary>
/// An immutable, ordered map of names to subschemas, for example the value of <c>properties</c> or <c>$defs</c>.
/// </summary>
/// <remarks>
/// - The map keeps the document order of its entries.
/// - It is immutable, so a keyword can share it without a copy.
/// - <c>foreach</c> over the map, <see cref="Keys"/> or <see cref="Values"/> uses a struct enumerator.
///   <see cref="TryGetValue(ReadOnlySpan{char}, out JsonSchema)"/> looks up a name without a string. Neither allocates.
/// - <see cref="Keys"/> and <see cref="Values"/> are struct views of the entries.
///   They are boxed only when you use them through <see cref="IEnumerable{T}"/>, for example with LINQ.
/// - A small map uses a linear search. A map with more than 16 entries also has an index.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1710:Identifiers should have correct suffix",
    Justification = "JSON Schema calls these values maps. SchemaDictionary would read worse."
)]
public sealed class SchemaMap : IReadOnlyDictionary<string, JsonSchema>, IEquatable<SchemaMap>
{
    //  Maps in a schema are usually small. A linear search of a few names is faster than a hash lookup.
    private const int MaxLinearSearch = 16;

    private readonly KeyValuePair<string, JsonSchema>[] entries;
    private readonly Dictionary<string, int>? index;

    private SchemaMap(KeyValuePair<string, JsonSchema>[] entries, Dictionary<string, int>? index)
    {
        this.entries = entries;
        this.index = index;
    }

    /// <summary>
    /// The empty map.
    /// </summary>
    public static SchemaMap Empty { get; } = new([], null);

    /// <summary>
    /// The number of entries.
    /// </summary>
    public int Count => entries.Length;

    /// <summary>
    /// The entries, in document order.
    /// </summary>
    public ReadOnlySpan<KeyValuePair<string, JsonSchema>> Entries => entries;

    /// <summary>
    /// The names, in document order.
    /// </summary>
    public KeyCollection Keys => new(entries);

    /// <summary>
    /// The subschemas, in document order.
    /// </summary>
    public ValueCollection Values => new(entries);

    IEnumerable<string> IReadOnlyDictionary<string, JsonSchema>.Keys => Keys;

    IEnumerable<JsonSchema> IReadOnlyDictionary<string, JsonSchema>.Values => Values;

    /// <summary>
    /// Gets the subschema with a name.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The map has no entry with that name.</exception>
    public JsonSchema this[string key] =>
        TryGetValue(key, out var schema) ? schema : throw new KeyNotFoundException($"The map has no entry named '{key}'.");

    /// <summary>
    /// Creates a map. The order of <paramref name="entries"/> is kept. A <see cref="SchemaMap"/> is returned as it is.
    /// </summary>
    /// <exception cref="ArgumentException">A name occurs more than once, or a name or schema is null.</exception>
    public static SchemaMap Create(IEnumerable<KeyValuePair<string, JsonSchema>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries is SchemaMap map)
        {
            return map;
        }

        var array = entries.ToArray();
        foreach (var (name, schema) in array)
        {
            ArgumentNullException.ThrowIfNull(name, nameof(entries));
            ArgumentNullException.ThrowIfNull(schema, nameof(entries));
        }

        return TryCreate(array, out var created, out var duplicate)
            ? created
            : throw new ArgumentException($"The name '{duplicate}' occurs more than once.", nameof(entries));
    }

    /// <summary>
    /// Creates a map. The order of <paramref name="entries"/> is kept.
    /// </summary>
    /// <exception cref="ArgumentException">A name occurs more than once, or a name or schema is null.</exception>
    public static SchemaMap Create(params ReadOnlySpan<(string Name, JsonSchema Schema)> entries)
    {
        var array = new KeyValuePair<string, JsonSchema>[entries.Length];
        for (var position = 0; position < entries.Length; position++)
        {
            var (name, schema) = entries[position];
            ArgumentNullException.ThrowIfNull(name, nameof(entries));
            ArgumentNullException.ThrowIfNull(schema, nameof(entries));
            array[position] = new(name, schema);
        }

        return TryCreate(array, out var created, out var duplicate)
            ? created
            : throw new ArgumentException($"The name '{duplicate}' occurs more than once.", nameof(entries));
    }

    /// <summary>
    /// Returns <see langword="true"/> if a map with <paramref name="count"/> entries uses an index.
    /// </summary>
    internal static bool NeedsIndex(int count) => count > MaxLinearSearch;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="entries"/> contains <paramref name="name"/>.
    /// </summary>
    internal static bool ContainsName(ReadOnlySpan<KeyValuePair<string, JsonSchema>> entries, string name)
    {
        foreach (var entry in entries)
        {
            if (string.Equals(entry.Key, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a map from entries that the caller has checked for unique names.
    /// The map uses the array and the index without a copy. The caller must not change them.
    /// </summary>
    /// <param name="entries">The entries, with unique names.</param>
    /// <param name="index">The index of the entries, if <see cref="NeedsIndex"/> is <see langword="true"/> for their count.</param>
    internal static SchemaMap FromUniqueEntries(KeyValuePair<string, JsonSchema>[] entries, Dictionary<string, int>? index) =>
        entries.Length == 0 ? Empty : new SchemaMap(entries, index);

    /// <summary>
    /// Creates a map that uses <paramref name="entries"/> without a copy. The caller must not change the array.
    /// </summary>
    internal static bool TryCreate(
        KeyValuePair<string, JsonSchema>[] entries,
        [NotNullWhen(true)] out SchemaMap? map,
        [NotNullWhen(false)] out string? duplicate
    )
    {
        if (entries.Length == 0)
        {
            map = Empty;
            duplicate = null;
            return true;
        }

        Dictionary<string, int>? index = null;
        if (entries.Length > MaxLinearSearch)
        {
            index = new Dictionary<string, int>(entries.Length, StringComparer.Ordinal);
            for (var position = 0; position < entries.Length; position++)
            {
                if (!index.TryAdd(entries[position].Key, position))
                {
                    map = null;
                    duplicate = entries[position].Key;
                    return false;
                }
            }
        }
        else
        {
            for (var position = 1; position < entries.Length; position++)
            {
                for (var earlier = 0; earlier < position; earlier++)
                {
                    if (string.Equals(entries[earlier].Key, entries[position].Key, StringComparison.Ordinal))
                    {
                        map = null;
                        duplicate = entries[position].Key;
                        return false;
                    }
                }
            }
        }

        map = new SchemaMap(entries, index);
        duplicate = null;
        return true;
    }

    /// <summary>
    /// Gets the subschema with a name.
    /// </summary>
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonSchema value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (index is not null)
        {
            var found = index.TryGetValue(key, out var position);
            value = found ? entries[position].Value : null;
            return found;
        }

        return TryGetValue(key.AsSpan(), out value);
    }

    /// <summary>
    /// Gets the subschema with a name, without a string for the name.
    /// </summary>
    public bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonSchema value)
    {
        if (index is not null)
        {
            var found = index.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key, out var position);
            value = found ? entries[position].Value : null;
            return found;
        }

        foreach (var (name, schema) in entries)
        {
            if (key.SequenceEqual(name))
            {
                value = schema;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the map has an entry with the name.
    /// </summary>
    public bool ContainsKey(string key) => TryGetValue(key, out _);

    /// <summary>
    /// Returns <see langword="true"/> if the two maps have the same names with equal subschemas.
    /// The order of the entries is not important, as for a JSON object.
    /// </summary>
    public bool Equals([NotNullWhen(true)] SchemaMap? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || other.Count != Count)
        {
            return false;
        }

        foreach (var (name, schema) in entries)
        {
            if (!other.TryGetValue(name, out var otherSchema) || !schema.Equals(otherSchema))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SchemaMap);

    /// <summary>
    /// Returns a hash code. Equal maps have the same hash code.
    /// </summary>
    public override int GetHashCode()
    {
        var sum = 0;
        foreach (var (name, schema) in entries)
        {
            sum = unchecked(sum + JsonValueEquality.Property(JsonValueEquality.String(name), schema.GetHashCode()));
        }

        return JsonValueEquality.ObjectHash(sum, entries.Length);
    }

    /// <summary>
    /// Returns an enumerator that does not allocate.
    /// </summary>
    public Enumerator GetEnumerator() => new(entries);

    IEnumerator<KeyValuePair<string, JsonSchema>> IEnumerable<KeyValuePair<string, JsonSchema>>.GetEnumerator() =>
        ((IEnumerable<KeyValuePair<string, JsonSchema>>)entries).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => entries.GetEnumerator();

    /// <summary>
    /// Enumerates the entries of a <see cref="SchemaMap"/> in document order.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator
    {
        private readonly KeyValuePair<string, JsonSchema>[] entries;
        private int position;

        internal Enumerator(KeyValuePair<string, JsonSchema>[] entries)
        {
            this.entries = entries;
            position = -1;
        }

        /// <summary>The current entry.</summary>
        public readonly KeyValuePair<string, JsonSchema> Current => entries[position];

        /// <summary>Moves to the next entry.</summary>
        public bool MoveNext() => ++position < entries.Length;
    }

    /// <summary>
    /// The names of a <see cref="SchemaMap"/>, in document order. It is a view of the map, not a copy.
    /// </summary>
    /// <remarks>
    /// The default value is an empty collection.
    /// </remarks>
    public readonly struct KeyCollection : IReadOnlyCollection<string>
    {
        private readonly KeyValuePair<string, JsonSchema>[]? entries;

        internal KeyCollection(KeyValuePair<string, JsonSchema>[] entries) => this.entries = entries;

        /// <inheritdoc/>
        public int Count => entries?.Length ?? 0;

        /// <summary>Returns an enumerator that does not allocate.</summary>
        public KeyEnumerator GetEnumerator() => new(entries ?? []);

        IEnumerator<string> IEnumerable<string>.GetEnumerator() =>
            (entries ?? []).Select(entry => entry.Key).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<string>)this).GetEnumerator();
    }

    /// <summary>
    /// Enumerates the names of a <see cref="SchemaMap"/>.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct KeyEnumerator
    {
        private Enumerator inner;

        internal KeyEnumerator(KeyValuePair<string, JsonSchema>[] entries) => inner = new Enumerator(entries);

        /// <summary>The current name.</summary>
        public readonly string Current => inner.Current.Key;

        /// <summary>Moves to the next name.</summary>
        public bool MoveNext() => inner.MoveNext();
    }

    /// <summary>
    /// The subschemas of a <see cref="SchemaMap"/>, in document order. It is a view of the map, not a copy.
    /// </summary>
    /// <remarks>
    /// The default value is an empty collection.
    /// </remarks>
    public readonly struct ValueCollection : IReadOnlyCollection<JsonSchema>
    {
        private readonly KeyValuePair<string, JsonSchema>[]? entries;

        internal ValueCollection(KeyValuePair<string, JsonSchema>[] entries) => this.entries = entries;

        /// <inheritdoc/>
        public int Count => entries?.Length ?? 0;

        /// <summary>Returns an enumerator that does not allocate.</summary>
        public ValueEnumerator GetEnumerator() => new(entries ?? []);

        IEnumerator<JsonSchema> IEnumerable<JsonSchema>.GetEnumerator() =>
            (entries ?? []).Select(entry => entry.Value).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<JsonSchema>)this).GetEnumerator();
    }

    /// <summary>
    /// Enumerates the subschemas of a <see cref="SchemaMap"/>.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct ValueEnumerator
    {
        private Enumerator inner;

        internal ValueEnumerator(KeyValuePair<string, JsonSchema>[] entries) => inner = new Enumerator(entries);

        /// <summary>The current subschema.</summary>
        public readonly JsonSchema Current => inner.Current.Value;

        /// <summary>Moves to the next subschema.</summary>
        public bool MoveNext() => inner.MoveNext();
    }
}
