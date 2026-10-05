namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Reads each configured provider and enforcement attribute catalog.
/// </summary>
public interface IAttributeCatalogClient
{
    /// <summary>
    /// Requests each app's catalog and collects successes and failures.
    /// Remote apps are HTTP GETs. Local apps read the matching in-process source.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the whole pull. A canceled token propagates.
    /// A client timeout, which cancels without this token, is recorded as that app failing.
    /// </param>
    /// <returns>Provider and enforcement apps whose rows were read, and the names of apps that were not.</returns>
    Task<AttributeCatalogPull> PullAsync(CancellationToken cancellationToken);
}
