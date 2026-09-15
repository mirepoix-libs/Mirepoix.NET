using Microsoft.Data.SqlClient;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

internal sealed class ResourceReader
{
    private readonly SqlConnectionFactory _connections;

    public ResourceReader(SqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<Resource> ReadAsync(string resourceType, string resourceId, CancellationToken cancellationToken)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.Ordinal);

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT name, value_json FROM dbo.{AccessControlSchema.ResourceAttributeTable}
             WHERE resource_type = @type AND resource_id = @id
             """;
        command.Parameters.AddWithValue("@type", resourceType);
        command.Parameters.AddWithValue("@id", resourceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var any = false;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            any = true;
            var name = reader.GetString(0);
            var json = reader.IsDBNull(1) ? null : reader.GetString(1);
            attributes[name] = AttributeValueCodec.FromJson(json);
        }

        if (!any)
            throw new KeyNotFoundException($"Resource '{resourceType}:{resourceId}' was not found.");

        return new Resource(resourceType, resourceId, attributes);
    }
}
