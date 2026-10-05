using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Keeps the last complete attribute union and records every pull, including failures.
/// </summary>
public sealed class AttributeCatalogSnapshot
{
    /// <summary>
    /// Gets whether a pull with no failed apps has been applied.
    /// </summary>
    public bool HasSnapshot { get; private set; }

    /// <summary>
    /// Gets the distinct attribute rows from the last complete pull, in first-seen order.
    /// Provider apps contribute subject and resource rows. Enforcement apps contribute context rows
    /// with a null type. A later pull that lists failed apps leaves this union unchanged.
    /// </summary>
    public IReadOnlyList<PublishedAttribute> Attributes { get; private set; } = [];

    /// <summary>
    /// Gets the most recent pull passed to <see cref="Apply"/>, including a pull that listed failed apps.
    /// Null until the first apply.
    /// </summary>
    public AttributeCatalogPull? LastPull { get; private set; }

    /// <summary>
    /// Stores <paramref name="pull"/> as <see cref="LastPull"/>.
    /// Replaces <see cref="Attributes"/> and sets <see cref="HasSnapshot"/>
    /// only when <see cref="AttributeCatalogPull.FailedApps"/> is empty.
    /// </summary>
    /// <param name="pull">Pull to record. A non-empty failure list leaves the stored union unchanged.</param>
    public void Apply(AttributeCatalogPull pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        LastPull = pull;
        if (pull.FailedApps.Count > 0)
            return;

        Attributes = Union(pull);
        HasSnapshot = true;
    }

    /// <summary>
    /// Chooses the union a validator should use for <paramref name="pull"/> without changing this snapshot.
    /// </summary>
    /// <param name="pull">Pull just completed.</param>
    /// <returns>
    /// The union of <paramref name="pull"/> when every app answered.
    /// The stored <see cref="Attributes"/> when some apps failed and <see cref="HasSnapshot"/> is true.
    /// </returns>
    /// <exception cref="AttributeCatalogUnavailableException">
    /// Thrown when <paramref name="pull"/> lists failed apps and no complete union is stored.
    /// </exception>
    public IReadOnlyList<PublishedAttribute> SelectAttributes(AttributeCatalogPull pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        if (pull.FailedApps.Count == 0)
            return Union(pull);

        if (HasSnapshot)
            return Attributes;

        throw new AttributeCatalogUnavailableException(
            "Attribute catalog snapshot is unavailable because the pull failed and no complete catalog is stored.");
    }

    private static List<PublishedAttribute> Union(AttributeCatalogPull pull)
    {
        var values = new List<PublishedAttribute>();
        var seen = new HashSet<AttributeKey>();
        foreach (var app in pull.ProviderApps)
        {
            foreach (var row in app.Attributes)
            {
                if (row.Target is not (AttributeTarget.Subject or AttributeTarget.Resource))
                    continue;

                Add(values, seen, row);
            }
        }

        foreach (var app in pull.EnforcementApps)
        {
            foreach (var row in app.Attributes)
            {
                if (row.Target != AttributeTarget.Context)
                    continue;

                Add(values, seen, new PublishedAttribute(AttributeTarget.Context, null, row.Key));
            }
        }

        return values;
    }

    private static void Add(List<PublishedAttribute> values, HashSet<AttributeKey> seen, PublishedAttribute row)
    {
        var type = row.Target == AttributeTarget.Context ? "" : row.Type ?? "";
        if (!seen.Add(new AttributeKey(row.Target, type, row.Key)))
            return;

        values.Add(row);
    }

    private readonly record struct AttributeKey(AttributeTarget Target, string Type, string Key);
}
