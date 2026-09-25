namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Names one enforcement app and the origin the catalog client GETs.
/// </summary>
/// <param name="Name">Configured app name recorded on pulls and failures.</param>
/// <param name="Origin">Scheme, host, and port. A trailing slash is removed when the catalog URL is built.</param>
public sealed record EnforcementApp(string Name, string Origin);

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
    /// Appends an enforcement app the client will GET.
    /// </summary>
    /// <param name="name">App name copied onto successful catalogs and <c>FailedApps</c>.</param>
    /// <param name="origin">Base origin. A trailing slash is ignored when the request URL is built.</param>
    public void AddApp(string name, string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);
        _apps.Add(new EnforcementApp(name, origin));
    }
}
