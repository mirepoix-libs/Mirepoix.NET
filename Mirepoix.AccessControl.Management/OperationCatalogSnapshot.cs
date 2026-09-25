using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Keeps the last complete operation union and records every pull, including failures.
/// </summary>
public sealed class OperationCatalogSnapshot
{
    /// <summary>
    /// Gets whether a pull with no failed apps has been applied.
    /// </summary>
    public bool HasSnapshot { get; private set; }

    /// <summary>
    /// Gets the longest published operation's segment count from the last complete pull.
    /// Zero when that union is empty or no complete pull has been applied.
    /// </summary>
    public int MaxSegments { get; private set; }

    /// <summary>
    /// Gets the distinct operation strings from the last complete pull, in first-seen order.
    /// Unchanged when a later pull lists failed apps.
    /// </summary>
    public IReadOnlyList<string> OperationValues { get; private set; } = [];

    /// <summary>
    /// Gets the most recent pull passed to <see cref="Apply"/>, including a pull that listed failed apps.
    /// Null until the first apply.
    /// </summary>
    public OperationCatalogPull? LastPull { get; private set; }

    /// <summary>
    /// Stores <paramref name="pull"/> as <see cref="LastPull"/>.
    /// Replaces <see cref="OperationValues"/> and <see cref="MaxSegments"/>, and sets <see cref="HasSnapshot"/>,
    /// only when <see cref="OperationCatalogPull.FailedApps"/> is empty.
    /// </summary>
    /// <param name="pull">Pull to record. A non-empty failure list leaves the stored union and max unchanged.</param>
    public void Apply(OperationCatalogPull pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        LastPull = pull;
        if (pull.FailedApps.Count > 0)
            return;

        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var max = 0;
        foreach (var app in pull.Apps)
        {
            foreach (var published in app.Operations)
            {
                if (!seen.Add(published.Operation))
                    continue;

                values.Add(published.Operation);
                var segments = Operation.Parse(published.Operation).Value.Split(':').Length;
                if (segments > max)
                    max = segments;
            }
        }

        OperationValues = values;
        MaxSegments = max;
        HasSnapshot = true;
    }
}
