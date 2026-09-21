namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds optional table/column hints for ADO-style adapters that build SQL from the map.
/// Adapters that already have a CLR model (and their own metadata) may ignore these.
/// </summary>
public sealed class SubjectStorageHints
{
    /// <summary>Names the optional schema; null means default/unspecified schema.</summary>
    public string? Schema { get; init; }

    /// <summary>Names the optional table; when null, adapters typically fall back to <see cref="SubjectEntityMap.ClrType"/> name.</summary>
    public string? Table { get; init; }

    /// <summary>
    /// Maps property name to column name. Unlisted properties use the property name as the column name.
    /// </summary>
    public IReadOnlyDictionary<string, string> ColumnOverrides { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
