using System.Data;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

/// <summary>
/// Fetches and materializes mapped subject entities via ADO.
/// Table defaults to <see cref="SubjectEntityMap.ClrType"/> name; schema defaults to <c>dbo</c>.
/// Column names come from <see cref="SubjectStorageHints.ColumnOverrides"/> or the property name.
/// Also loads library-owned role rows for mapped layouts that retain <c>ac_subject_role</c>.
/// Returns null when no row matches (probe miss). Bracket-escapes identifiers by doubling <c>]</c>.
/// Id parameter is coerced to the id property's CLR type (string, Guid, or ChangeType).
/// </summary>
internal sealed class MappedSubjectReader
{
    private readonly SqlConnectionFactory _connections;

    public MappedSubjectReader(SqlConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>
    /// Selects a single row by id for <paramref name="map"/> and materializes <see cref="SubjectEntityMap.ClrType"/>.
    /// </summary>
    /// <returns>Entity instance, or null when no row.</returns>
    public async Task<object?> ReadAsync(
        SubjectEntityMap map,
        string subjectId,
        CancellationToken cancellationToken)
    {
        var table = ResolveTable(map);
        var schema = map.StorageHints?.Schema ?? "dbo";
        var idColumn = ResolveColumn(map, map.IdMember);

        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT * FROM [{Escape(schema)}].[{Escape(table)}]
             WHERE [{Escape(idColumn)}] = @id
             """;
        command.Parameters.Add(CreateIdParameter("@id", subjectId, map.IdMember.PropertyType));

        await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken)
            .ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return Materialize(reader, map);
    }

    /// <summary>
    /// Loads library-owned role rows for a mapped subject without requiring an <c>ac_subject</c> header row.
    /// </summary>
    public async Task<IReadOnlyList<string>> ReadRolesAsync(
        string subjectId,
        CancellationToken cancellationToken)
    {
        await using var connection = _connections.Create();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT role FROM dbo.{AccessControlSchema.SubjectRoleTable}
            WHERE subject_id = @id
            """;
        command.Parameters.AddWithValue("@id", subjectId);

        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            roles.Add(reader.GetString(0));

        return roles;
    }

    /// <summary>Resolves table name from storage hints or CLR type name.</summary>
    internal static string ResolveTable(SubjectEntityMap map)
    {
        var table = map.StorageHints?.Table;
        if (!string.IsNullOrWhiteSpace(table))
            return table;

        return map.ClrType.Name;
    }

    /// <summary>Resolves column name from overrides or property name.</summary>
    internal static string ResolveColumn(SubjectEntityMap map, PropertyInfo property)
    {
        if (map.StorageHints?.ColumnOverrides.TryGetValue(property.Name, out var column) == true
            && !string.IsNullOrWhiteSpace(column))
        {
            return column;
        }

        return property.Name;
    }

    /// <summary>
    /// Creates <see cref="SubjectEntityMap.ClrType"/> via parameterless activator and sets writable
    /// public properties from matching columns (ordinal ignore-case). Missing columns are skipped.
    /// DBNull sets null on nullable/reference properties and leaves non-nullable value types unchanged.
    /// </summary>
    internal static object Materialize(IDataRecord record, SubjectEntityMap map)
    {
        var entity = Activator.CreateInstance(map.ClrType)
            ?? throw new InvalidOperationException($"Could not create instance of '{map.ClrType.Name}'.");

        var properties = map.ClrType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0);

        foreach (var prop in properties)
        {
            var column = ResolveColumn(map, prop);
            var ordinal = TryGetOrdinal(record, column);
            if (ordinal < 0)
                continue;

            if (record.IsDBNull(ordinal))
            {
                if (!prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) is not null)
                    prop.SetValue(entity, null);
                continue;
            }

            var raw = record.GetValue(ordinal);
            prop.SetValue(entity, ConvertValue(raw, prop.PropertyType));
        }

        return entity;
    }

    private static int TryGetOrdinal(IDataRecord record, string column)
    {
        for (var i = 0; i < record.FieldCount; i++)
        {
            if (string.Equals(record.GetName(i), column, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static object? ConvertValue(object raw, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(raw))
            return raw;
        if (underlying == typeof(Guid) && raw is string s)
            return Guid.Parse(s);
        if (underlying.IsEnum)
            return Enum.Parse(underlying, Convert.ToString(raw, CultureInfo.InvariantCulture)!, ignoreCase: true);

        return Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
    }

    private static SqlParameter CreateIdParameter(string name, string subjectId, Type propertyType)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        object value = underlying switch
        {
            _ when underlying == typeof(string) => subjectId,
            _ when underlying == typeof(Guid) => Guid.Parse(subjectId),
            _ => Convert.ChangeType(subjectId, underlying, CultureInfo.InvariantCulture)!,
        };

        return new SqlParameter(name, value);
    }

    private static string Escape(string identifier) => identifier.Replace("]", "]]", StringComparison.Ordinal);
}
