namespace Mirepoix.AccessControl.Management.Server.Http;

/// <summary>
/// Selects management HTTP slices and their shared route settings.
/// </summary>
public sealed class AccessControlManagementServerHttpOptions
{
    /// <summary>Gets or sets the shared route prefix.</summary>
    public string RoutePrefix { get; set; } = "/access-control";

    /// <summary>Gets or sets the optional authorization policy applied to every management endpoint.</summary>
    public string? AuthorizationPolicy { get; set; }

    private readonly List<(string Name, string Origin)> _enforcementApps = [];

    internal bool SubjectsEnabled { get; private set; }
    internal bool RolesEnabled { get; private set; }
    internal bool SodEnabled { get; private set; }
    internal bool PolicySetEnabled { get; private set; }

    /// <summary>
    /// Gets enforcement apps in registration order. Empty when catalog pull and the aggregate GET stay off.
    /// </summary>
    internal IReadOnlyList<(string Name, string Origin)> EnforcementApps => _enforcementApps;

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

    /// <summary>Enables policy-set replacement.</summary>
    public AccessControlManagementServerHttpOptions AddPolicySet()
    {
        PolicySetEnabled = true;
        return this;
    }

    /// <summary>
    /// Appends an enforcement app. A non-empty list registers the operation catalog and maps <c>GET /operations</c>.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>failedApps</c>.</param>
    /// <param name="origin">Scheme, host, and port. A trailing slash is removed when the catalog URL is built.</param>
    /// <returns>This options instance.</returns>
    public AccessControlManagementServerHttpOptions AddEnforcementApp(string name, string origin)
    {
        _enforcementApps.Add((name, origin));
        return this;
    }
}
