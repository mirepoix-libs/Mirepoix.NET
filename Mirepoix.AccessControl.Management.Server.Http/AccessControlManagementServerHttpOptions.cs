using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management.Server.Http;

/// <summary>
/// Selects management HTTP slices and their shared route settings.
/// </summary>
public sealed class AccessControlManagementServerHttpOptions
{
    /// <summary>Gets or sets the shared route prefix.</summary>
    public string RoutePrefix { get; set; } = "/access-control";

    private readonly List<ManagementCatalogApp> _enforcementApps = [];
    private readonly List<ManagementCatalogApp> _providerApps = [];
    private readonly List<PolicyModel> _pins = [];

    internal bool SubjectsEnabled { get; private set; }
    internal bool RolesEnabled { get; private set; }
    internal bool SodEnabled { get; private set; }
    internal bool PolicySetEnabled { get; private set; }

    /// <summary>
    /// Gets enforcement apps in registration order. A null origin is a local app.
    /// Empty when operation and context-attribute pulls stay off.
    /// </summary>
    internal IReadOnlyList<ManagementCatalogApp> EnforcementApps => _enforcementApps;

    /// <summary>
    /// Gets provider apps in registration order. A null origin is a local app.
    /// Empty when no provider app was registered.
    /// </summary>
    internal IReadOnlyList<ManagementCatalogApp> ProviderApps => _providerApps;

    /// <summary>Gets pins in registration order. Empty when the host did not call <see cref="PinPolicy"/>.</summary>
    internal IReadOnlyList<PolicyModel> Pins => _pins;

    /// <summary>
    /// Appends <paramref name="policy"/> so it is part of the effective set and not stored.
    /// </summary>
    /// <param name="policy">Allow or deny copied onto reads and stripped from exact replaces.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">Thrown when another pin already uses the same id (ordinal).</exception>
    public AccessControlManagementServerHttpOptions PinPolicy(PolicyModel policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        foreach (var existing in _pins)
        {
            if (string.Equals(existing.Id, policy.Id, StringComparison.Ordinal))
                throw new ArgumentException($"Pinned policy '{policy.Id}' is already registered.");
        }

        _pins.Add(policy);
        return this;
    }

    /// <summary>Enables subject management endpoints.</summary>
    public AccessControlManagementServerHttpOptions AddSubjects()
    {
        SubjectsEnabled = true;
        return this;
    }

    /// <summary>Enables role catalog endpoints.</summary>
    public AccessControlManagementServerHttpOptions AddRoles()
    {
        RolesEnabled = true;
        return this;
    }

    /// <summary>Enables separation-of-duty constraint endpoints.</summary>
    public AccessControlManagementServerHttpOptions AddSod()
    {
        SodEnabled = true;
        return this;
    }

    /// <summary>Enables policy-set read and replacement.</summary>
    public AccessControlManagementServerHttpOptions AddPolicySet()
    {
        PolicySetEnabled = true;
        return this;
    }

    /// <summary>
    /// Appends a remote enforcement app.
    /// A non-empty enforcement list registers the operation catalog and the attribute catalog,
    /// and maps <c>GET /operations</c> plus <c>GET /attributes</c>.
    /// The operation pull and the context-attribute pull both use this name and origin.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>failedApps</c>.</param>
    /// <param name="origin">Scheme, host, and port. A trailing slash is removed when the catalog URL is built.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="origin"/> is null or blank,
    /// or when <paramref name="name"/> is already in <see cref="EnforcementApps"/>.
    /// </exception>
    public AccessControlManagementServerHttpOptions AddEnforcementApp(string name, string origin)
    {
        AddRemote(_enforcementApps, name, origin);
        return this;
    }

    /// <summary>
    /// Appends a local enforcement app read from in-process operation and context-attribute sources.
    /// <paramref name="name"/> defaults to <c>local</c>. No HTTP client is used for this entry.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>failedApps</c>.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null or blank, or when it is already in <see cref="EnforcementApps"/>.
    /// </exception>
    public AccessControlManagementServerHttpOptions AddLocalEnforcementApp(string name = "local")
    {
        AddLocal(_enforcementApps, name);
        return this;
    }

    /// <summary>
    /// Appends a remote provider app.
    /// A non-empty provider list registers the attribute catalog and maps <c>GET /attributes</c>.
    /// Subject and resource rows come from this origin.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>failedApps</c>.</param>
    /// <param name="origin">Scheme, host, and port. A trailing slash is removed when the catalog URL is built.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="origin"/> is null or blank,
    /// or when <paramref name="name"/> is already in <see cref="ProviderApps"/>.
    /// </exception>
    public AccessControlManagementServerHttpOptions AddProviderApp(string name, string origin)
    {
        AddRemote(_providerApps, name, origin);
        return this;
    }

    /// <summary>
    /// Appends a local provider app read from the in-process subject and resource attribute source.
    /// <paramref name="name"/> defaults to <c>local</c>. No HTTP client is used for this entry.
    /// The same default name may also be used for <see cref="AddLocalEnforcementApp"/> because the lists are separate.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>failedApps</c>.</param>
    /// <returns>This options instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null or blank, or when it is already in <see cref="ProviderApps"/>.
    /// </exception>
    public AccessControlManagementServerHttpOptions AddLocalProviderApp(string name = "local")
    {
        AddLocal(_providerApps, name);
        return this;
    }

    private static void AddRemote(List<ManagementCatalogApp> apps, string name, string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        EnsureUnique(apps, name);
        apps.Add(new ManagementCatalogApp(name, origin));
    }

    private static void AddLocal(List<ManagementCatalogApp> apps, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        EnsureUnique(apps, name);
        apps.Add(new ManagementCatalogApp(name, null));
    }

    private static void EnsureUnique(List<ManagementCatalogApp> apps, string name)
    {
        foreach (var app in apps)
        {
            if (string.Equals(app.Name, name, StringComparison.Ordinal))
                throw new ArgumentException($"An app named '{name}' is already registered.", nameof(name));
        }
    }
}

/// <summary>
/// Names one management catalog app. A null <see cref="Origin"/> is read from DI instead of HTTP.
/// </summary>
/// <param name="Name">Configured app name recorded on pulls and failures.</param>
/// <param name="Origin">Scheme, host, and port for a remote app. Null for a local app.</param>
internal sealed record ManagementCatalogApp(string Name, string? Origin)
{
    /// <summary>Gets whether this app is read in-process.</summary>
    public bool IsLocal => Origin is null;
}
