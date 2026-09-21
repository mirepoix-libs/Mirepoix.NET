using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Resolves resources as an <see cref="IResourceResolver"/> by loading attributes from
/// <c>ac_resource_attribute</c> for the partial's type and id. Zero matching rows means not found
/// (no separate resource header table).
/// </summary>
public sealed class SqlServerResourceResolver : IResourceResolver
{
    private readonly ResourceReader _reader;

    /// <summary>
    /// Creates a resolver from <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Connection string source.</param>
    public SqlServerResourceResolver(SqlServerProviderOptions options)
        : this(new ResourceReader(new SqlConnectionFactory(options.ConnectionString)))
    {
    }

    /// <summary>
    /// Creates a resolver with an injected reader (package/tests).
    /// </summary>
    internal SqlServerResourceResolver(ResourceReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Hydrates resource attributes for <paramref name="partial"/>.
    /// </summary>
    /// <param name="partial">Resource type and id (attributes on the partial are ignored).</param>
    /// <param name="cancellationToken">Cancellation for SQL.</param>
    /// <returns>Resource with stored attributes.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no attribute rows exist for the identity.</exception>
    public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken) =>
        _reader.ReadAsync(partial.Type, partial.Id, cancellationToken);
}
