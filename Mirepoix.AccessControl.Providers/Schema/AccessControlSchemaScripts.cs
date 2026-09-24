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

    /// <summary>
    /// Loads core, subject-role, management, native-subject, then legacy resource-drop scripts in application order.
    /// </summary>
    /// <param name="dialect">Target database dialect.</param>
    /// <returns>
    /// Ordered script text corresponding to
    /// <see cref="AccessControlSchemaDialectMap.ResourceNames(AccessControlSchemaDialect)"/>.
    /// </returns>
    public static IReadOnlyList<string> Load(AccessControlSchemaDialect dialect) =>
        AccessControlSchemaDialectMap.ResourceNames(dialect).Select(Load).ToArray();

    /// <summary>
    /// Loads the complete SqlServer schema as one compatibility script in application order.
    /// </summary>
    public static string LoadSqlServerInit() =>
        string.Join(Environment.NewLine, Load(AccessControlSchemaDialect.SqlServer));

    /// <summary>
    /// Loads the complete PostgreSQL schema as one compatibility script in application order.
    /// </summary>
    public static string LoadPostgreSqlInit() =>
        string.Join(Environment.NewLine, Load(AccessControlSchemaDialect.PostgreSql));

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
