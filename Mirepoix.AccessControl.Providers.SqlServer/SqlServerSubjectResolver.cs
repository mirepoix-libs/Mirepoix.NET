using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Resolves subjects as an <see cref="ISubjectResolver"/> via SqlServer ADO.
/// With empty subject maps: loads from <c>ac_subject*</c>.
/// With maps: uses <see cref="SubjectMappingLookup"/> and ADO materialization of mapped tables, then unions
/// <c>ac_subject_role</c> rows when role storage remains library-owned.
/// Validates maps with <c>requireStorageTable: true</c> at construction when maps are present.
/// </summary>
public sealed class SqlServerSubjectResolver : ISubjectResolver
{
    private readonly SubjectReader _reader;
    private readonly MappedSubjectReader? _mappedReader;
    private readonly SubjectMappingOptions _subjectMapping;
    private readonly SubjectStorageLayout _layout;

    /// <summary>
    /// Creates a resolver from <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Connection string and optional subject maps.</param>
    public SqlServerSubjectResolver(SqlServerProviderOptions options)
        : this(
            new SubjectReader(new SqlConnectionFactory(options.ConnectionString)),
            options.SubjectMapping.HasMaps
                ? new MappedSubjectReader(new SqlConnectionFactory(options.ConnectionString))
                : null,
            options.SubjectMapping)
    {
        options.SubjectMapping.Validate(requireStorageTable: options.SubjectMapping.HasMaps);
    }

    /// <summary>
    /// Creates a resolver with injected readers (package/tests).
    /// </summary>
    internal SqlServerSubjectResolver(
        SubjectReader reader,
        MappedSubjectReader? mappedReader,
        SubjectMappingOptions subjectMapping)
    {
        _reader = reader;
        _mappedReader = mappedReader;
        _subjectMapping = subjectMapping;
        _layout = SubjectStorageLayoutResolver.Resolve(subjectMapping);
    }

    /// <summary>
    /// Hydrates <paramref name="partial"/> from built-in tables or mapped entities. Mapped layouts without
    /// explicit role members union library-owned role rows with roles produced by the entity mapping.
    /// </summary>
    /// <param name="partial">Partial subject (id required; optional type hint attribute when mapped).</param>
    /// <param name="cancellationToken">Cancellation for SQL.</param>
    /// <returns>Hydrated subject.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the subject cannot be found.</exception>
    public Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken)
    {
        if (!_subjectMapping.HasMaps)
            return _reader.ReadAsync(partial.Id, cancellationToken);

        if (_mappedReader is null)
            throw new InvalidOperationException("Mapped subject reader was not configured.");

        return HydrateMappedAsync(partial, cancellationToken);
    }

    private async Task<Subject> HydrateMappedAsync(
        Subject partial,
        CancellationToken cancellationToken)
    {
        var subject = await SubjectMappingLookup.HydrateAsync(
                partial,
                _subjectMapping,
                (map, id, ct) => _mappedReader!.ReadAsync(map, id, ct),
                cancellationToken)
            .ConfigureAwait(false);

        if (_layout != SubjectStorageLayout.MappedLibraryRoles)
            return subject;

        var libraryRoles = await _mappedReader!
            .ReadRolesAsync(subject.Id, cancellationToken)
            .ConfigureAwait(false);
        return MergeLibraryRoles(subject, libraryRoles);
    }

    /// <summary>
    /// Unions mapped and library-owned roles with ordinal comparison while preserving mapped attributes.
    /// </summary>
    internal static Subject MergeLibraryRoles(
        Subject subject,
        IEnumerable<string> libraryRoles)
    {
        var roles = new HashSet<string>(subject.Roles, StringComparer.Ordinal);
        foreach (var role in libraryRoles)
            roles.Add(role);

        return roles.Count == subject.Roles.Count
            ? subject
            : subject with { Roles = roles };
    }
}
