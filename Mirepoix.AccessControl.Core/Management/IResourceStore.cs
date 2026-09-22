namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores resource attributes and ownership.
/// </summary>
public interface IResourceStore
{
    /// <summary>
    /// Sets a resource attribute asynchronously.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="key">Names the attribute.</param>
    /// <param name="value">Supplies the attribute value, including <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task SetAttributeAsync(string resourceType, string resourceId, string key, object? value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a resource attribute asynchronously.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="key">Names the attribute to clear.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task ClearAttributeAsync(string resourceType, string resourceId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resource attributes asynchronously.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Returns the resource's attributes.</returns>
    Task<IReadOnlyDictionary<string, object?>> GetAttributesAsync(string resourceType, string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a resource owner asynchronously.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="ownerSubjectId">Identifies the owning subject.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task SetOwnerAsync(string resourceType, string resourceId, string ownerSubjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a resource owner asynchronously.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task ClearOwnerAsync(string resourceType, string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a resource owner asynchronously.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Returns the owning subject identifier, or <see langword="null"/> when no owner is set.</returns>
    Task<string?> GetOwnerAsync(string resourceType, string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a resource attribute.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="key">Names the attribute.</param>
    /// <param name="value">Supplies the attribute value, including <see langword="null"/>.</param>
    void SetAttribute(string resourceType, string resourceId, string key, object? value);

    /// <summary>
    /// Clears a resource attribute.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="key">Names the attribute to clear.</param>
    void ClearAttribute(string resourceType, string resourceId, string key);

    /// <summary>
    /// Gets resource attributes.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <returns>Returns the resource's attributes.</returns>
    IReadOnlyDictionary<string, object?> GetAttributes(string resourceType, string resourceId);

    /// <summary>
    /// Sets a resource owner.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <param name="ownerSubjectId">Identifies the owning subject.</param>
    void SetOwner(string resourceType, string resourceId, string ownerSubjectId);

    /// <summary>
    /// Clears a resource owner.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    void ClearOwner(string resourceType, string resourceId);

    /// <summary>
    /// Gets a resource owner.
    /// </summary>
    /// <param name="resourceType">Identifies the resource type.</param>
    /// <param name="resourceId">Identifies the resource within its type.</param>
    /// <returns>Returns the owning subject identifier, or <see langword="null"/> when no owner is set.</returns>
    string? GetOwner(string resourceType, string resourceId);
}
