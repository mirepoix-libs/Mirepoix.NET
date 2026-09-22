using Mirepoix.AccessControl.Management;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Mirepoix.AccessControl.Management.Server.Http.Tests;

public sealed class ManagementEndpointMappingTests
{
    [Fact]
    public void MapAccessControlManagement_AllowsScopedSeamInDevelopment()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.Services.AddScoped<ISubjectStore, FakeSubjectStore>();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddSubjects());
        using var app = builder.Build();

        app.MapAccessControlManagement();
    }

    [Fact]
    public void MapAccessControlManagement_ThrowsWhenEnabledSubjectsSeamIsMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddSubjects());
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => app.MapAccessControlManagement());

        Assert.Contains("Subjects management HTTP slice requires ISubjectStore.", exception.Message);
    }

    [Fact]
    public async Task SubjectsSlice_ManagesSubjectAttributesAndRoles()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ISubjectStore, FakeSubjectStore>();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddSubjects());
        await using var app = builder.Build();
        app.MapAccessControlManagement();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var create = await client.PostAsync("/access-control/subjects/alice", null);
        var setAttribute = await client.PutAsJsonAsync(
            "/access-control/subjects/alice/attributes/department",
            "engineering");
        var assignRole = await client.PostAsync(
            "/access-control/subjects/alice/roles/editor",
            null);
        var roles = await client.GetFromJsonAsync<string[]>(
            "/access-control/subjects/alice/roles");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, setAttribute.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, assignRole.StatusCode);
        Assert.Equal(["editor"], roles);
    }

    [Fact]
    public async Task SodSlice_PostsJsonConstraint()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<SodState>();
        builder.Services.AddScoped<ISodConstraintStore, FakeSodConstraintStore>();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddSod());
        await using var app = builder.Build();
        app.MapAccessControlManagement();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/access-control/sod",
            new { id = "sod-1", mutuallyExclusiveRoles = new[] { "author", "approver" } });
        var constraints = await client.GetFromJsonAsync<SodResponse[]>("/access-control/sod");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var constraint = Assert.Single(constraints!);
        Assert.Equal("sod-1", constraint.Id);
        Assert.Equal(["approver", "author"], constraint.MutuallyExclusiveRoles.Order());
    }

    [Fact]
    public async Task SubjectRoleRoutes_DoNotRequireSubjectHeaderStorage()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<RoleState>();
        builder.Services.AddScoped<ISubjectStore, RoleOnlySubjectStore>();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddSubjects());
        await using var app = builder.Build();
        app.MapAccessControlManagement();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var assign = await client.PostAsync(
            "/access-control/subjects/library-user/roles/editor",
            null);
        var roles = await client.GetFromJsonAsync<string[]>(
            "/access-control/subjects/library-user/roles");

        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        Assert.Equal(["editor"], roles);
    }

    [Fact]
    public async Task ResourcesSlice_ManagesResourceAttributes()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ResourceState>();
        builder.Services.AddScoped<IResourceStore, FakeResourceStore>();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddResources());
        await using var app = builder.Build();
        app.MapAccessControlManagement();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var update = await client.PutAsJsonAsync(
            "/access-control/resources/document/report-1/attributes/classification",
            "confidential");
        var attributes = await client.GetFromJsonAsync<Dictionary<string, string>>(
            "/access-control/resources/document/report-1/attributes");

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal("confidential", attributes!["classification"]);
    }

    private sealed class FakeSubjectStore : ISubjectStore
    {
        private readonly Dictionary<string, Dictionary<string, object?>> _attributes = [];
        private readonly Dictionary<string, HashSet<string>> _roles = [];

        public Task CreateAsync(string subjectId, CancellationToken cancellationToken = default)
        {
            Create(subjectId);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string subjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Exists(subjectId));

        public Task DeleteAsync(string subjectId, CancellationToken cancellationToken = default)
        {
            Delete(subjectId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ListIds());

        public Task SetAttributeAsync(string subjectId, string key, object? value, CancellationToken cancellationToken = default)
        {
            SetAttribute(subjectId, key, value);
            return Task.CompletedTask;
        }

        public Task ClearAttributeAsync(string subjectId, string key, CancellationToken cancellationToken = default)
        {
            ClearAttribute(subjectId, key);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, object?>> GetAttributesAsync(string subjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAttributes(subjectId));

        public Task<AssignmentResult> AssignRoleAsync(string subjectId, string roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AssignRole(subjectId, roleId));

        public Task RevokeRoleAsync(string subjectId, string roleId, CancellationToken cancellationToken = default)
        {
            RevokeRole(subjectId, roleId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlySet<string>> GetRolesAsync(string subjectId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetRoles(subjectId));

        public void Create(string subjectId)
        {
            _attributes.Add(subjectId, []);
            _roles.Add(subjectId, []);
        }

        public bool Exists(string subjectId) => _attributes.ContainsKey(subjectId);

        public void Delete(string subjectId)
        {
            _attributes.Remove(subjectId);
            _roles.Remove(subjectId);
        }

        public IReadOnlyList<string> ListIds() => _attributes.Keys.ToArray();

        public void SetAttribute(string subjectId, string key, object? value) =>
            _attributes[subjectId][key] = value;

        public void ClearAttribute(string subjectId, string key) =>
            _attributes[subjectId].Remove(key);

        public IReadOnlyDictionary<string, object?> GetAttributes(string subjectId) =>
            _attributes[subjectId];

        public AssignmentResult AssignRole(string subjectId, string roleId)
        {
            _roles[subjectId].Add(roleId);
            return AssignmentResult.Assigned();
        }

        public void RevokeRole(string subjectId, string roleId) =>
            _roles[subjectId].Remove(roleId);

        public IReadOnlySet<string> GetRoles(string subjectId) =>
            _roles[subjectId];
    }

    private sealed class SodState
    {
        public Dictionary<string, SodConstraint> Constraints { get; } = [];
    }

    private sealed record SodResponse(string Id, string[] MutuallyExclusiveRoles);

    private sealed class FakeSodConstraintStore(SodState state) : ISodConstraintStore
    {
        public void Add(SodConstraint constraint) =>
            state.Constraints[constraint.Id] = constraint;

        public void Remove(string id) =>
            state.Constraints.Remove(id);

        public IReadOnlyList<SodConstraint> List() =>
            state.Constraints.Values.ToArray();

        public Task AddAsync(SodConstraint constraint, CancellationToken cancellationToken = default)
        {
            Add(constraint);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
        {
            Remove(id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SodConstraint>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(List());
    }

    private sealed class RoleState
    {
        public Dictionary<string, HashSet<string>> Roles { get; } = [];
    }

    private sealed class RoleOnlySubjectStore(RoleState state) : ISubjectStore
    {
        public Task<AssignmentResult> AssignRoleAsync(
            string subjectId,
            string roleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AssignRole(subjectId, roleId));

        public AssignmentResult AssignRole(string subjectId, string roleId)
        {
            GetOrCreateRoles(subjectId).Add(roleId);
            return AssignmentResult.Assigned();
        }

        public Task RevokeRoleAsync(
            string subjectId,
            string roleId,
            CancellationToken cancellationToken = default)
        {
            RevokeRole(subjectId, roleId);
            return Task.CompletedTask;
        }

        public void RevokeRole(string subjectId, string roleId) =>
            GetOrCreateRoles(subjectId).Remove(roleId);

        public Task<IReadOnlySet<string>> GetRolesAsync(
            string subjectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GetRoles(subjectId));

        public IReadOnlySet<string> GetRoles(string subjectId) =>
            GetOrCreateRoles(subjectId);

        public Task<bool> ExistsAsync(string subjectId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Subject header storage is unavailable.");

        public bool Exists(string subjectId) =>
            throw new InvalidOperationException("Subject header storage is unavailable.");

        public Task CreateAsync(string subjectId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Create(string subjectId) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string subjectId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Delete(string subjectId) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ListIdsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IReadOnlyList<string> ListIds() =>
            throw new NotSupportedException();

        public Task SetAttributeAsync(
            string subjectId,
            string key,
            object? value,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void SetAttribute(string subjectId, string key, object? value) =>
            throw new NotSupportedException();

        public Task ClearAttributeAsync(
            string subjectId,
            string key,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ClearAttribute(string subjectId, string key) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, object?>> GetAttributesAsync(
            string subjectId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IReadOnlyDictionary<string, object?> GetAttributes(string subjectId) =>
            throw new NotSupportedException();

        private HashSet<string> GetOrCreateRoles(string subjectId)
        {
            if (!state.Roles.TryGetValue(subjectId, out var roles))
            {
                roles = [];
                state.Roles.Add(subjectId, roles);
            }

            return roles;
        }
    }

    private sealed class ResourceState
    {
        public Dictionary<(string Type, string Id), Dictionary<string, object?>> Attributes { get; } = [];

        public Dictionary<(string Type, string Id), string> Owners { get; } = [];
    }

    private sealed class FakeResourceStore(ResourceState state) : IResourceStore
    {
        public Task SetAttributeAsync(
            string resourceType,
            string resourceId,
            string key,
            object? value,
            CancellationToken cancellationToken = default)
        {
            SetAttribute(resourceType, resourceId, key, value);
            return Task.CompletedTask;
        }

        public void SetAttribute(string resourceType, string resourceId, string key, object? value) =>
            GetOrCreateAttributes(resourceType, resourceId)[key] = value;

        public Task ClearAttributeAsync(
            string resourceType,
            string resourceId,
            string key,
            CancellationToken cancellationToken = default)
        {
            ClearAttribute(resourceType, resourceId, key);
            return Task.CompletedTask;
        }

        public void ClearAttribute(string resourceType, string resourceId, string key) =>
            GetOrCreateAttributes(resourceType, resourceId).Remove(key);

        public Task<IReadOnlyDictionary<string, object?>> GetAttributesAsync(
            string resourceType,
            string resourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAttributes(resourceType, resourceId));

        public IReadOnlyDictionary<string, object?> GetAttributes(string resourceType, string resourceId) =>
            GetOrCreateAttributes(resourceType, resourceId);

        public Task SetOwnerAsync(
            string resourceType,
            string resourceId,
            string ownerSubjectId,
            CancellationToken cancellationToken = default)
        {
            SetOwner(resourceType, resourceId, ownerSubjectId);
            return Task.CompletedTask;
        }

        public void SetOwner(string resourceType, string resourceId, string ownerSubjectId) =>
            state.Owners[(resourceType, resourceId)] = ownerSubjectId;

        public Task ClearOwnerAsync(
            string resourceType,
            string resourceId,
            CancellationToken cancellationToken = default)
        {
            ClearOwner(resourceType, resourceId);
            return Task.CompletedTask;
        }

        public void ClearOwner(string resourceType, string resourceId) =>
            state.Owners.Remove((resourceType, resourceId));

        public Task<string?> GetOwnerAsync(
            string resourceType,
            string resourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GetOwner(resourceType, resourceId));

        public string? GetOwner(string resourceType, string resourceId) =>
            state.Owners.GetValueOrDefault((resourceType, resourceId));

        private Dictionary<string, object?> GetOrCreateAttributes(string resourceType, string resourceId)
        {
            var key = (resourceType, resourceId);
            if (!state.Attributes.TryGetValue(key, out var attributes))
            {
                attributes = [];
                state.Attributes.Add(key, attributes);
            }

            return attributes;
        }
    }
}
