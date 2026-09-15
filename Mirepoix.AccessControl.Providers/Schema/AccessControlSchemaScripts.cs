using System.Text;

namespace Mirepoix.AccessControl.Providers.Schema;

public static class AccessControlSchemaScripts
{
    public static string Load(string resourceName)
    {
        var assembly = typeof(AccessControlSchemaScripts).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded schema script '{resourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static string LoadSqlServerInit() =>
        Load(AccessControlSchemaDialectMap.SqlServerInitResource);

    public static string LoadPostgreSqlInit() =>
        Load(AccessControlSchemaDialectMap.PostgreSqlInitResource);

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
