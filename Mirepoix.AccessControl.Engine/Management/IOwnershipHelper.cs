namespace Mirepoix.AccessControl.Management;

public interface IOwnershipHelper
{
    void SetOwner(string resourceType, string resourceId, string ownerSubjectId);

    void ClearOwner(string resourceType, string resourceId);
}
