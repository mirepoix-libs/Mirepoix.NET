using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Resolves subjects as an <see cref="ISubjectResolver"/> via SqlServer ADO.
/// With empty <see cref="SqlServerProviderOptions.SubjectMapping"/>: loads from <c>ac_subject*</c>.
/// With maps: uses <see cref="SubjectMappingLookup"/> and ADO materialization of mapped tables.
/// Validates maps with <c>requireStorageTable: true</c> at construction when maps are present.
/// </summary>
public sealed class SqlServerSubjectResolver : ISubjectResolver
{
    private readonly SubjectReader _reader;
    private readonly MappedSubjectReader? _mappedReader;
    private readonly SubjectMappingOptions _subjectMapping;

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
    }

    /// <summary>
    /// Hydrates <paramref name="partial"/> from built-in tables or mapped entities.
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

        return SubjectMappingLookup.HydrateAsync(
            partial,
            _subjectMapping,
            (map, id, ct) => _mappedReader.ReadAsync(map, id, ct),
            cancellationToken);
    }
}
