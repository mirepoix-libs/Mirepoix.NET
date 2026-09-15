using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

public sealed class SqlServerSubjectResolver : ISubjectResolver
{
    private readonly SubjectReader _reader;

    public SqlServerSubjectResolver(SqlServerProviderOptions options)
        : this(new SubjectReader(new SqlConnectionFactory(options.ConnectionString)))
    {
    }

    internal SqlServerSubjectResolver(SubjectReader reader)
    {
        _reader = reader;
    }

    public Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken) =>
        _reader.ReadAsync(partial.Id, cancellationToken);
}
