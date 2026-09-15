namespace Mirepoix.AccessControl.Management;

public interface IRoleAssignmentStore
{
    AssignmentResult Assign(string subjectId, string roleId);

    void Revoke(string subjectId, string roleId);

    IReadOnlySet<string> GetRoles(string subjectId);
}
