namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Names one enforcement app and where its operation catalog is read.
/// A null <see cref="Origin"/> marks a local app read from DI.
/// </summary>
/// <param name="Name">Configured app name recorded on pulls and failures.</param>
/// <param name="Origin">Scheme, host, and port for a remote app. Null for a local app. A trailing slash is removed when the catalog URL is built.</param>
public sealed record EnforcementApp(string Name, string? Origin)
{
    /// <summary>
    /// Gets whether this app is read from an in-process catalog source instead of HTTP.
    /// </summary>
    public bool IsLocal => Origin is null;
}

/// <summary>
/// Stores enforcement app origins for catalog pulls.
/// </summary>
public sealed class OperationCatalogOptions
{
    private readonly List<EnforcementApp> _apps = [];

    /// <summary>
    /// Lists apps in the order <see cref="EnforcementCatalogClient"/> requests them.
    /// </summary>
    public IReadOnlyList<EnforcementApp> Apps => _apps;

    /// <summary>
    /// Appends a remote enforcement app the client will GET.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>FailedApps</c>.</param>
    /// <param name="origin">Base origin. A trailing slash is ignored when the request URL is built.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="origin"/> is null or blank,
    /// or when <paramref name="name"/> is already in <see cref="Apps"/>.
    /// </exception>
    public void AddApp(string name, string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        EnsureUnique(name);
        _apps.Add(new EnforcementApp(name, origin));
    }

    /// <summary>
    /// Appends a local enforcement app read from <c>IPublishedOperationSource</c>.
    /// </summary>
    /// <param name="name">App name. Defaults to <c>local</c>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null or blank, or when it is already in <see cref="Apps"/>.
    /// </exception>
    public void AddLocalEnforcementApp(string name = "local")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureUnique(name);
        _apps.Add(new EnforcementApp(name, null));
    }

    private void EnsureUnique(string name)
    {
        foreach (var app in _apps)
        {
            if (string.Equals(app.Name, name, StringComparison.Ordinal))
                throw new ArgumentException($"An app named '{name}' is already registered.", nameof(name));
        }
    }
}
