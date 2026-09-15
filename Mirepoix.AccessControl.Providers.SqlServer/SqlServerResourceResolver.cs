using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

public sealed class SqlServerResourceResolver : IResourceResolver
{
    private readonly ResourceReader _reader;

    public SqlServerResourceResolver(SqlServerProviderOptions options)
        : this(new ResourceReader(new SqlConnectionFactory(options.ConnectionString)))
    {
    }

    internal SqlServerResourceResolver(ResourceReader reader)
    {
        _reader = reader;
    }

    public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken) =>
        _reader.ReadAsync(partial.Type, partial.Id, cancellationToken);
}
