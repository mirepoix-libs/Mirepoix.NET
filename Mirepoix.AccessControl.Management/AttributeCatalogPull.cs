using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Carries one app's published attributes from a successful catalog read.
/// </summary>
/// <param name="Name">Configured app name.</param>
/// <param name="Attributes">Rows kept for that app's role. Empty when the app published nothing that role accepts.</param>
public sealed record AttributeAppCatalog(string Name, IReadOnlyList<PublishedAttribute> Attributes);

/// <summary>
/// Carries the apps that answered and the names that failed on one attribute pull.
/// </summary>
/// <param name="ProviderApps">Provider apps whose subject and resource rows were read. Failed apps are omitted.</param>
/// <param name="EnforcementApps">Enforcement apps whose context rows were read. Failed apps are omitted.</param>
/// <param name="FailedApps">App names that did not answer. Empty when every configured app answered.</param>
public sealed record AttributeCatalogPull(
    IReadOnlyList<AttributeAppCatalog> ProviderApps,
    IReadOnlyList<AttributeAppCatalog> EnforcementApps,
    IReadOnlyList<string> FailedApps);
