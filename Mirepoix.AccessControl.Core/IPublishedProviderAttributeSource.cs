namespace Mirepoix.AccessControl;

/// <summary>
/// Lists subject and resource attribute keys from an in-process provider app.
/// Management pulls local provider apps through this seam without referencing Providers.Server.Http.
/// </summary>
public interface IPublishedProviderAttributeSource
{
    /// <summary>
    /// Returns the live subject/resource attribute catalog the provider GET would return.
    /// </summary>
    /// <returns>Published attributes with subject or resource target only.</returns>
    IReadOnlyList<PublishedAttribute> List();
}
