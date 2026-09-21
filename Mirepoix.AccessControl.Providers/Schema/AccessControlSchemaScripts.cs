using System.Text;

namespace Mirepoix.AccessControl.Providers.Schema;

/// <summary>
/// Loads embedded dialect init scripts and splits them into executable batches.
/// Scripts live as embedded resources in this assembly (see <see cref="AccessControlSchemaDialectMap"/>).
/// </summary>
public static class AccessControlSchemaScripts
{
    /// <summary>
    /// Reads an embedded resource by exact logical name.
    /// </summary>
    /// <param name="resourceName">Manifest resource name (e.g. <see cref="AccessControlSchemaDialectMap.SqlServerInitResource"/>).</param>
    /// <returns>Full script text (UTF-8).</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource is missing.</exception>
    public static string Load(string resourceName)
    {
        var assembly = typeof(AccessControlSchemaScripts).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded schema script '{resourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Loads the SqlServer <c>001_init.sql</c> script.</summary>
    public static string LoadSqlServerInit() =>
        Load(AccessControlSchemaDialectMap.SqlServerInitResource);

    /// <summary>Loads the PostgreSQL <c>001_init.sql</c> script.</summary>
    public static string LoadPostgreSqlInit() =>
        Load(AccessControlSchemaDialectMap.PostgreSqlInitResource);

    /// <summary>
    /// Splits <paramref name="script"/> into batches for the given dialect.
    /// SqlServer: split on lines that are only <c>GO</c> (ordinal ignore case); empty batches dropped.
    /// PostgreSql: one batch (trimmed whole script) when non-empty.
    /// </summary>
    /// <param name="script">Full script text.</param>
    /// <param name="dialect">Dialect that owns the script conventions.</param>
    /// <returns>Non-empty batches in order.</returns>
    public static IEnumerable<string> SplitBatches(string script, AccessControlSchemaDialect dialect)
    {
        if (dialect == AccessControlSchemaDialect.PostgreSql)
        {
            var trimmed = script.Trim();
            if (trimmed.Length > 0)
                yield return trimmed;
            yield break;
        }

        var current = new StringBuilder();
        using var reader = new StringReader(script);
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                var batch = current.ToString().Trim();
                if (batch.Length > 0)
                    yield return batch;
                current.Clear();
                continue;
            }

            current.AppendLine(line);
        }

        var tail = current.ToString().Trim();
        if (tail.Length > 0)
            yield return tail;
    }
}
