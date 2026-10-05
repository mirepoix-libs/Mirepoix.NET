namespace Mirepoix.AccessControl;

/// <summary>
/// Lists published operations from an in-process enforcement app.
/// Management pulls local enforcement apps through this seam without referencing AspNetCore.
/// </summary>
public interface IPublishedOperationSource
{
    /// <summary>
    /// Returns the live operation catalog the enforcement GET would return.
    /// </summary>
    /// <returns>Published operations in catalog order.</returns>
    IReadOnlyList<PublishedOperation> List();
}
