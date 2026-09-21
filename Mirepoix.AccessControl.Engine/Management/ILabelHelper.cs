namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Sets or clears arbitrary attribute labels on subjects and resources for evaluation atoms.
/// Management-plane helper; not called by the checker.
/// </summary>
public interface ILabelHelper
{
    /// <summary>
    /// Sets a subject attribute.
    /// </summary>
    /// <param name="subjectId">Subject id.</param>
    /// <param name="key">Attribute key.</param>
    /// <param name="value">Attribute value; may be null.</param>
    void SetSubjectLabel(string subjectId, string key, object? value);

    /// <summary>
    /// Removes a subject attribute key if the subject exists.
    /// </summary>
    /// <param name="subjectId">Subject id.</param>
    /// <param name="key">Attribute key to remove.</param>
    void ClearSubjectLabel(string subjectId, string key);

    /// <summary>
    /// Sets a resource attribute.
    /// </summary>
    /// <param name="resourceType">Resource type.</param>
    /// <param name="resourceId">Resource id.</param>
    /// <param name="key">Attribute key.</param>
    /// <param name="value">Attribute value; may be null.</param>
    void SetResourceLabel(string resourceType, string resourceId, string key, object? value);

    /// <summary>
    /// Removes a resource attribute key if the resource exists.
    /// </summary>
    /// <param name="resourceType">Resource type.</param>
    /// <param name="resourceId">Resource id.</param>
    /// <param name="key">Attribute key to remove.</param>
    void ClearResourceLabel(string resourceType, string resourceId, string key);
}
