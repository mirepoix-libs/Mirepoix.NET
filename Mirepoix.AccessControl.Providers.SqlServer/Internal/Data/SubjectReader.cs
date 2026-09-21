using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

/// <summary>
/// Hydrates subjects from <c>dbo.ac_subject</c>, roles, and attributes.
/// Requires a row in <c>ac_subject</c>; missing row throws even if role/attribute rows somehow exist.
/// Attribute values decode via <see cref="AttributeValueCodec"/>.
/// </summary>
internal sealed class SubjectReader
{
    private readonly SqlConnectionFactory _connections;

    public SubjectReader(SqlConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>
    /// Loads roles and attributes for <paramref name="subjectId"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the subject id is not in <c>ac_subject</c>.</exception>
    public async Task<Subject> ReadAsync(string subjectId, CancellationToken cancellationToken)
    {
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var exists = connection.CreateCommand())
        {
            exists.CommandText =
                $"""
                 SELECT 1 FROM dbo.{AccessControlSchema.SubjectTable}
                 WHERE subject_id = @id
                 """;
            exists.Parameters.AddWithValue("@id", subjectId);
            var found = await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (found is null)
                throw new KeyNotFoundException($"Subject '{subjectId}' was not found.");
        }

        var roles = new HashSet<string>(StringComparer.Ordinal);
        await using (var roleCmd = connection.CreateCommand())
        {
            roleCmd.CommandText =
                $"""
                 SELECT role FROM dbo.{AccessControlSchema.SubjectRoleTable}
                 WHERE subject_id = @id
                 """;
            roleCmd.Parameters.AddWithValue("@id", subjectId);
            await using var roleReader = await roleCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await roleReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                roles.Add(roleReader.GetString(0));
        }

        var attributes = new Dictionary<string, object?>(StringComparer.Ordinal);
        await using (var attrCmd = connection.CreateCommand())
        {
            attrCmd.CommandText =
                $"""
                 SELECT name, value_json FROM dbo.{AccessControlSchema.SubjectAttributeTable}
                 WHERE subject_id = @id
                 """;
            attrCmd.Parameters.AddWithValue("@id", subjectId);
            await using var attrReader = await attrCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await attrReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = attrReader.GetString(0);
                var json = attrReader.IsDBNull(1) ? null : attrReader.GetString(1);
                attributes[name] = AttributeValueCodec.FromJson(json);
            }
        }

        return new Subject(subjectId, roles, attributes);
    }
}
