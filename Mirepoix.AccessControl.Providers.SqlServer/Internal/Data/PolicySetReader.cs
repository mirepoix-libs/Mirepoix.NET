using Microsoft.Data.SqlClient;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

internal sealed class PolicySetReader
{
    private readonly SqlConnectionFactory _connections;

    public PolicySetReader(SqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<PolicySet> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT version, payload_json
             FROM dbo.{AccessControlSchema.PolicySetTable}
             WHERE id = @id
             """;
        command.Parameters.AddWithValue("@id", AccessControlSchema.PolicySetSingletonId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("No policy set row is configured in ac_policy_set.");

        var json = reader.GetString(1);
        return PolicySetStorageCodec.FromStorageJson(json);
    }
}
