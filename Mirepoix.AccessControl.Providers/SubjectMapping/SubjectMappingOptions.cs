namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds ordered subject entity maps shared by durable provider adapters.
/// Empty <see cref="Maps"/> means adapters should use built-in <c>ac_subject*</c> tables instead of mapped CLR types.
/// Mapped mode does not merge with <c>ac_subject*</c>: either maps are configured, or the built-in path is used.
/// </summary>
public sealed class SubjectMappingOptions
{
    /// <summary>Names the default hint attribute: <c>subjectType</c>.</summary>
    public const string DefaultHintAttributeName = "subjectType";

    private readonly List<SubjectEntityMap> _maps = new();

    /// <summary>
    /// Names the partial-subject attribute used as a type hint to skip probing other maps.
    /// Defaults to <see cref="DefaultHintAttributeName"/>.
    /// </summary>
    public string HintAttributeName { get; set; } = DefaultHintAttributeName;

    /// <summary>Lists configured maps in registration order (probe order when no hint).</summary>
    public IReadOnlyList<SubjectEntityMap> Maps => _maps;

    /// <summary>Reports <see langword="true"/> when at least one map is configured.</summary>
    public bool HasMaps => _maps.Count > 0;

    /// <summary>
    /// Adds a map for <typeparamref name="T"/> via a fluent builder and returns this instance for chaining.
    /// </summary>
    /// <typeparam name="T">CLR entity type.</typeparam>
    /// <param name="configure">Builder configuration (must set id unless <c>Id</c> property exists by convention).</param>
    /// <returns>This options instance.</returns>
    public SubjectMappingOptions MapEntity<T>(Action<SubjectEntityMapBuilder<T>> configure)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new SubjectEntityMapBuilder<T>();
        configure(builder);
        _maps.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Validates configured maps. Call at registration or first options use.
    /// </summary>
    /// <param name="requireStorageTable">
    /// When true, each map must resolve a table name from <see cref="SubjectStorageHints.Table"/>
    /// or the CLR type name (for ADO adapters).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a map lacks an id member, duplicate <see cref="SubjectEntityMap.FixedTypeValue"/> values exist,
    /// or a required table name cannot be resolved.
    /// </exception>
    public void Validate(bool requireStorageTable = false)
    {
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var map in _maps)
        {
            if (map.IdMember is null)
                throw new InvalidOperationException($"Subject map for '{map.ClrType.Name}' is missing an id member.");

            if (map.FixedTypeValue is not null)
            {
                if (!seenTypes.Add(map.FixedTypeValue))
                {
                    throw new InvalidOperationException(
                        $"Duplicate subject type value '{map.FixedTypeValue}'. Each FixedTypeValue must be unique.");
                }
            }

            if (requireStorageTable)
            {
                var table = map.StorageHints?.Table;
                if (string.IsNullOrWhiteSpace(table))
                    table = map.ClrType.Name;

                if (string.IsNullOrWhiteSpace(table))
                {
                    throw new InvalidOperationException(
                        $"Subject map for '{map.ClrType.Name}' needs a table name (ToTable) or a usable type name convention.");
                }
            }
        }
    }
}
