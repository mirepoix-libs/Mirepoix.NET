using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Carries one app's published operations from a successful catalog GET.
/// </summary>
/// <param name="Name">Configured app name.</param>
/// <param name="Operations">Deserialized catalog body. Empty when the app published nothing.</param>
public sealed record EnforcementAppCatalog(string Name, IReadOnlyList<PublishedOperation> Operations);

/// <summary>
/// Carries the apps that answered and the names that failed on one pull.
/// </summary>
/// <param name="Apps">Apps whose bodies deserialized. Failed apps are omitted.</param>
/// <param name="FailedApps">App names that timed out, returned a non-success status, or did not deserialize. Empty when every app answered.</param>
public sealed record OperationCatalogPull(
    IReadOnlyList<EnforcementAppCatalog> Apps,
    IReadOnlyList<string> FailedApps);
