namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Names one provider or enforcement app in an attribute-catalog pull.
/// A null <see cref="Origin"/> marks a local app read from DI.
/// </summary>
/// <param name="Name">Configured app name recorded on pulls and failures.</param>
/// <param name="Origin">Scheme, host, and port for a remote app. Null for a local app. A trailing slash is removed when the catalog URL is built.</param>
public sealed record AttributeCatalogApp(string Name, string? Origin)
{
    /// <summary>
    /// Gets whether this app is read from an in-process catalog source instead of HTTP.
    /// </summary>
    public bool IsLocal => Origin is null;
}

/// <summary>
/// Stores provider and enforcement apps for attribute-catalog pulls.
/// </summary>
public sealed class AttributeCatalogOptions
{
    private readonly List<AttributeCatalogApp> _providerApps = [];
    private readonly List<AttributeCatalogApp> _enforcementApps = [];

    /// <summary>
    /// Lists provider apps in the order <see cref="AttributeCatalogClient"/> requests them.
    /// Subject and resource rows come from these apps.
    /// </summary>
    public IReadOnlyList<AttributeCatalogApp> ProviderApps => _providerApps;

    /// <summary>
    /// Lists enforcement apps in the order <see cref="AttributeCatalogClient"/> requests them.
    /// Context rows come from these apps.
    /// </summary>
    public IReadOnlyList<AttributeCatalogApp> EnforcementApps => _enforcementApps;

    /// <summary>
    /// Gets or sets a callback applied to the named attribute-catalog <see cref="HttpClient"/>
    /// when <see cref="AttributeCatalogServiceCollectionExtensions.AddAccessControlAttributeCatalog"/> runs.
    /// Use it to attach caller credentials. Null skips extra configuration.
    /// </summary>
    public Action<HttpClient>? ConfigureHttpClient { get; set; }

    /// <summary>
    /// Appends a remote provider app the client will GET.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>FailedApps</c>.</param>
    /// <param name="origin">Base origin. A trailing slash is ignored when the request URL is built.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="origin"/> is null or blank,
    /// or when <paramref name="name"/> is already in <see cref="ProviderApps"/>.
    /// </exception>
    public void AddProviderApp(string name, string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        EnsureUnique(_providerApps, name);
        _providerApps.Add(new AttributeCatalogApp(name, origin));
    }

    /// <summary>
    /// Appends a local provider app read from <c>IPublishedProviderAttributeSource</c>.
    /// </summary>
    /// <param name="name">App name. Defaults to <c>local</c>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null or blank, or when it is already in <see cref="ProviderApps"/>.
    /// </exception>
    public void AddLocalProviderApp(string name = "local")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureUnique(_providerApps, name);
        _providerApps.Add(new AttributeCatalogApp(name, null));
    }

    /// <summary>
    /// Appends a remote enforcement app whose context attributes the client will GET.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>FailedApps</c>.</param>
    /// <param name="origin">Base origin. A trailing slash is ignored when the request URL is built.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="origin"/> is null or blank,
    /// or when <paramref name="name"/> is already in <see cref="EnforcementApps"/>.
    /// </exception>
    public void AddEnforcementApp(string name, string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        EnsureUnique(_enforcementApps, name);
        _enforcementApps.Add(new AttributeCatalogApp(name, origin));
    }

    /// <summary>
    /// Appends a local enforcement app read from <c>IPublishedEnforcementAttributeSource</c>.
    /// </summary>
    /// <param name="name">App name. Defaults to <c>local</c>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null or blank, or when it is already in <see cref="EnforcementApps"/>.
    /// </exception>
    public void AddLocalEnforcementApp(string name = "local")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureUnique(_enforcementApps, name);
        _enforcementApps.Add(new AttributeCatalogApp(name, null));
    }

    private static void EnsureUnique(List<AttributeCatalogApp> apps, string name)
    {
        foreach (var app in apps)
        {
            if (string.Equals(app.Name, name, StringComparison.Ordinal))
                throw new ArgumentException($"An app named '{name}' is already registered.", nameof(name));
        }
    }
}
