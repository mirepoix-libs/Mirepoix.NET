namespace Mirepoix.AccessControl.Management;

public interface ILabelHelper
{
    void SetSubjectLabel(string subjectId, string key, object? value);

    void ClearSubjectLabel(string subjectId, string key);

    void SetResourceLabel(string resourceType, string resourceId, string key, object? value);

    void ClearResourceLabel(string resourceType, string resourceId, string key);
}
