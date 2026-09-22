namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores subject identities, attributes, and role assignments.
/// </summary>
public interface ISubjectStore
{
    /// <summary>
    /// Creates a subject asynchronously.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to create.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task CreateAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines asynchronously whether a subject exists.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to find.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Returns <see langword="true"/> when the subject exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> ExistsAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subject asynchronously.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to delete.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task DeleteAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists subject identifiers asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Returns a snapshot of stored subject identifiers.</returns>
    Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a subject attribute asynchronously.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to update.</param>
    /// <param name="key">Names the attribute.</param>
    /// <param name="value">Supplies the attribute value, including <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task SetAttributeAsync(string subjectId, string key, object? value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a subject attribute asynchronously.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to update.</param>
    /// <param name="key">Names the attribute to clear.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task ClearAttributeAsync(string subjectId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets subject attributes asynchronously.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Returns the subject's attributes.</returns>
    Task<IReadOnlyDictionary<string, object?>> GetAttributesAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a role to a subject asynchronously.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to update.</param>
    /// <param name="roleId">Identifies the role to assign.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Returns the assignment outcome, including any separation-of-duty conflict.</returns>
    Task<AssignmentResult> AssignRoleAsync(string subjectId, string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a role from a subject asynchronously.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to update.</param>
    /// <param name="roleId">Identifies the role to revoke.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task RevokeRoleAsync(string subjectId, string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a subject's assigned roles asynchronously.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Returns the subject's assigned role identifiers.</returns>
    Task<IReadOnlySet<string>> GetRolesAsync(string subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a subject.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to create.</param>
    void Create(string subjectId);

    /// <summary>
    /// Determines whether a subject exists.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to find.</param>
    /// <returns>Returns <see langword="true"/> when the subject exists; otherwise, <see langword="false"/>.</returns>
    bool Exists(string subjectId);

    /// <summary>
    /// Deletes a subject.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to delete.</param>
    void Delete(string subjectId);

    /// <summary>
    /// Lists subject identifiers.
    /// </summary>
    /// <returns>Returns a snapshot of stored subject identifiers.</returns>
    IReadOnlyList<string> ListIds();

    /// <summary>
    /// Sets a subject attribute.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to update.</param>
    /// <param name="key">Names the attribute.</param>
    /// <param name="value">Supplies the attribute value, including <see langword="null"/>.</param>
    void SetAttribute(string subjectId, string key, object? value);

    /// <summary>
    /// Clears a subject attribute.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to update.</param>
    /// <param name="key">Names the attribute to clear.</param>
    void ClearAttribute(string subjectId, string key);

    /// <summary>
    /// Gets subject attributes.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to read.</param>
    /// <returns>Returns the subject's attributes.</returns>
    IReadOnlyDictionary<string, object?> GetAttributes(string subjectId);

    /// <summary>
    /// Assigns a role to a subject.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to update.</param>
    /// <param name="roleId">Identifies the role to assign.</param>
    /// <returns>Returns the assignment outcome, including any separation-of-duty conflict.</returns>
    AssignmentResult AssignRole(string subjectId, string roleId);

    /// <summary>
    /// Revokes a role from a subject.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to update.</param>
    /// <param name="roleId">Identifies the role to revoke.</param>
    void RevokeRole(string subjectId, string roleId);

    /// <summary>
    /// Gets a subject's assigned roles.
    /// </summary>
    /// <param name="subjectId">Identifies the subject to read.</param>
    /// <returns>Returns the subject's assigned role identifiers.</returns>
    IReadOnlySet<string> GetRoles(string subjectId);
}
