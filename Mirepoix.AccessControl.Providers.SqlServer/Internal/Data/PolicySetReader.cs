using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

/// <summary>
/// Reads the singleton <c>dbo.ac_policy_set</c> row (<see cref="AccessControlSchema.PolicySetSingletonId"/>)
/// and deserializes <c>payload_json</c> via <see cref="PolicySetStorageCodec"/>. Schema is hard-coded to <c>dbo</c>.
/// </summary>
internal sealed class PolicySetReader
{
    private readonly SqlConnectionFactory _connections;

    public PolicySetReader(SqlConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>
    /// Loads the current policy set. Throws when the singleton row is missing.
    /// </summary>
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
