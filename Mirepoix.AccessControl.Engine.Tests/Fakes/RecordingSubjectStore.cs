using Mirepoix.AccessControl.Management;

namespace Mirepoix.AccessControl.Engine.Tests.Fakes;

internal sealed class RecordingSubjectStore : ISubjectStore
{
    public string? AssignedSubjectId { get; private set; }

    public string? AssignedRoleId { get; private set; }

    public Task CreateAsync(string subjectId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<bool> ExistsAsync(string subjectId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task DeleteAsync(string subjectId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task SetAttributeAsync(
        string subjectId,
        string key,
        object? value,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task ClearAttributeAsync(
        string subjectId,
        string key,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyDictionary<string, object?>> GetAttributesAsync(
        string subjectId,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<AssignmentResult> AssignRoleAsync(
        string subjectId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        AssignedSubjectId = subjectId;
        AssignedRoleId = roleId;
        return Task.FromResult(AssignmentResult.Assigned());
    }

    public Task RevokeRoleAsync(
        string subjectId,
        string roleId,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlySet<string>> GetRolesAsync(
        string subjectId,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public void Create(string subjectId) => throw new NotImplementedException();

    public bool Exists(string subjectId) => throw new NotImplementedException();

    public void Delete(string subjectId) => throw new NotImplementedException();

    public IReadOnlyList<string> ListIds() => throw new NotImplementedException();

    public void SetAttribute(string subjectId, string key, object? value) =>
        throw new NotImplementedException();

    public void ClearAttribute(string subjectId, string key) => throw new NotImplementedException();

    public IReadOnlyDictionary<string, object?> GetAttributes(string subjectId) =>
        throw new NotImplementedException();

    public AssignmentResult AssignRole(string subjectId, string roleId)
    {
        AssignedSubjectId = subjectId;
        AssignedRoleId = roleId;
        return AssignmentResult.Assigned();
    }

    public void RevokeRole(string subjectId, string roleId) => throw new NotImplementedException();

    public IReadOnlySet<string> GetRoles(string subjectId) => throw new NotImplementedException();
}
