namespace Mirepoix.AccessControl;

/// <summary>
/// Lists context attribute keys from an in-process enforcement app.
/// Management pulls local enforcement apps through this seam without referencing AspNetCore.
/// </summary>
public interface IPublishedEnforcementAttributeSource
{
    /// <summary>
    /// Returns the live context attribute catalog the enforcement GET would return.
    /// </summary>
    /// <returns>Published attributes with context target only.</returns>
    IReadOnlyList<PublishedAttribute> List();
}
