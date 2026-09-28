using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management.Server.Http;

/// <summary>
/// Selects management HTTP slices and their shared route settings.
/// </summary>
public sealed class AccessControlManagementServerHttpOptions
{
    /// <summary>Gets or sets the shared route prefix.</summary>
    public string RoutePrefix { get; set; } = "/access-control";

    private readonly List<(string Name, string Origin)> _enforcementApps = [];
    private readonly List<PolicyModel> _pins = [];

    internal bool SubjectsEnabled { get; private set; }
    internal bool RolesEnabled { get; private set; }
    internal bool SodEnabled { get; private set; }
    internal bool PolicySetEnabled { get; private set; }

    /// <summary>
    /// Gets enforcement apps in registration order. Empty when catalog pull and the aggregate GET stay off.
    /// </summary>
    internal IReadOnlyList<(string Name, string Origin)> EnforcementApps => _enforcementApps;

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
