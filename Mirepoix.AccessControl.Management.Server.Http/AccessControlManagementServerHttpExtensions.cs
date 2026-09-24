using System.Text.Json;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Management.Server.Http;

/// <summary>
/// Registers and maps opt-in AccessControl management HTTP slices.
/// </summary>
public static class AccessControlManagementServerHttpExtensions
{
    /// <summary>Registers management HTTP server options.</summary>
    public static IServiceCollection AddAccessControlManagementServerHttp(
        this IServiceCollection services,
        Action<AccessControlManagementServerHttpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AccessControlManagementServerHttpOptions();
        configure(options);
        services.AddSingleton(options);
        return services;
    }

    /// <summary>Maps all enabled management HTTP slices.</summary>
    public static IEndpointRouteBuilder MapAccessControlManagement(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<AccessControlManagementServerHttpOptions>();
        ValidateSeams(endpoints.ServiceProvider, options);

        var group = endpoints.MapGroup(options.RoutePrefix);
        if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        if (options.SubjectsEnabled)
        {
            MapSubjects(group, options.RoutePrefix);
        }

        if (options.RolesEnabled)
        {
            MapRoles(group, options.RoutePrefix);
        }

        if (options.SodEnabled)
        {
            MapSod(group, options.RoutePrefix);
        }

        if (options.PolicySetEnabled)
        {
            MapPolicySet(group);
        }

        return endpoints;
    }

    private static void MapSubjects(RouteGroupBuilder group, string routePrefix)
    {
        group.MapPost("/subjects/{id}", async (string id, ISubjectStore store, CancellationToken cancellationToken) =>
        {
            await store.CreateAsync(id, cancellationToken);
            return Results.Created($"{routePrefix}/subjects/{id}", new { id });
        });

        group.MapGet("/subjects/{id}", async (string id, ISubjectStore store, CancellationToken cancellationToken) =>
            await store.ExistsAsync(id, cancellationToken)
                ? Results.Ok(new { id })
                : Results.NotFound());

        group.MapDelete("/subjects/{id}", async (string id, ISubjectStore store, CancellationToken cancellationToken) =>
        {
            if (!await store.ExistsAsync(id, cancellationToken))
            {
                return Results.NotFound();
            }

            await store.DeleteAsync(id, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/subjects", async (ISubjectStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.ListIdsAsync(cancellationToken)));

        group.MapPut("/subjects/{id}/attributes/{key}", async (
            string id,
            string key,
            JsonElement value,
            ISubjectStore store,
            CancellationToken cancellationToken) =>
        {
            if (!await store.ExistsAsync(id, cancellationToken))
            {
                return Results.NotFound();
            }

            await store.SetAttributeAsync(id, key, ToClrValue(value), cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/subjects/{id}/attributes/{key}", async (
            string id,
            string key,
            ISubjectStore store,
            CancellationToken cancellationToken) =>
        {
            if (!await store.ExistsAsync(id, cancellationToken))
            {
                return Results.NotFound();
            }

            await store.ClearAttributeAsync(id, key, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/subjects/{id}/attributes", async (
            string id,
            ISubjectStore store,
            CancellationToken cancellationToken) =>
        {
            if (!await store.ExistsAsync(id, cancellationToken))
            {
                return Results.NotFound();
            }

            return Results.Ok(await store.GetAttributesAsync(id, cancellationToken));
        });

        group.MapPost("/subjects/{id}/roles/{roleId}", async (
            string id,
            string roleId,
            ISubjectStore store,
            CancellationToken cancellationToken) =>
        {
            var result = await store.AssignRoleAsync(id, roleId, cancellationToken);
            return result.Outcome == AssignmentOutcome.Assigned
                ? Results.NoContent()
                : Results.Conflict(result);
        });

        group.MapDelete("/subjects/{id}/roles/{roleId}", async (
            string id,
            string roleId,
            ISubjectStore store,
            CancellationToken cancellationToken) =>
        {
            await store.RevokeRoleAsync(id, roleId, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/subjects/{id}/roles", async (
            string id,
            ISubjectStore store,
            CancellationToken cancellationToken) =>
            Results.Ok(await store.GetRolesAsync(id, cancellationToken)));
    }

    private static void MapRoles(RouteGroupBuilder group, string routePrefix)
    {
        group.MapGet("/roles", async (IRoleCatalog catalog, CancellationToken cancellationToken) =>
            Results.Ok(await catalog.ListAsync(cancellationToken)));

        group.MapPost("/roles", async (Role role, IRoleCatalog catalog, CancellationToken cancellationToken) =>
        {
            await catalog.AddAsync(role, cancellationToken);
            return Results.Created($"{routePrefix}/roles/{role.Id}", role);
        });

        group.MapPut("/roles/{id}", async (
            string id,
            Role role,
            IRoleCatalog catalog,
            CancellationToken cancellationToken) =>
        {
            var replacement = new Role(id, role.Description);
            await catalog.AddAsync(replacement, cancellationToken);
            return Results.Ok(replacement);
        });

        group.MapDelete("/roles/{id}", async (string id, IRoleCatalog catalog, CancellationToken cancellationToken) =>
        {
            await catalog.RemoveAsync(id, cancellationToken);
            return Results.NoContent();
        });
    }

    private static void MapSod(RouteGroupBuilder group, string routePrefix)
    {
        group.MapGet("/sod", async (ISodConstraintStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.ListAsync(cancellationToken)));

        group.MapPost("/sod", async (
            SodConstraintRequest request,
            ISodConstraintStore store,
            CancellationToken cancellationToken) =>
        {
            var constraint = request.ToConstraint();
            await store.AddAsync(constraint, cancellationToken);
            return Results.Created($"{routePrefix}/sod/{constraint.Id}", constraint);
        });

        group.MapPut("/sod/{id}", async (
            string id,
            SodConstraintRequest request,
            ISodConstraintStore store,
            CancellationToken cancellationToken) =>
        {
            var replacement = request.ToConstraint(id);
            await store.AddAsync(replacement, cancellationToken);
            return Results.Ok(replacement);
        });

        group.MapDelete("/sod/{id}", async (
            string id,
            ISodConstraintStore store,
            CancellationToken cancellationToken) =>
        {
            await store.RemoveAsync(id, cancellationToken);
            return Results.NoContent();
        });
    }

    private static void MapPolicySet(RouteGroupBuilder group)
    {
        group.MapPut("/policy-set", async (
            HttpRequest request,
            IPolicySetEditor editor,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync(cancellationToken);

            try
            {
                var policySet = PolicySerializers.FromJson(json);
                await editor.ReplaceAsync(policySet, cancellationToken);
                return Results.NoContent();
            }
            catch (JsonException exception)
            {
                return Results.Problem(
                    exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });
    }

    private static object? ToClrValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt32(out var integer) ? integer
                : value.TryGetInt64(out var longInteger) ? longInteger
                : value.GetDouble(),
            JsonValueKind.Array => value.EnumerateArray().Select(ToClrValue).ToArray(),
            JsonValueKind.Object => value.EnumerateObject()
                .ToDictionary(property => property.Name, property => ToClrValue(property.Value)),
            _ => value.Clone(),
        };

    private static void ValidateSeams(IServiceProvider services, AccessControlManagementServerHttpOptions options)
    {
        var serviceProbe = services.GetRequiredService<IServiceProviderIsService>();

        if (options.SubjectsEnabled && !serviceProbe.IsService(typeof(ISubjectStore)))
        {
            throw new InvalidOperationException(
                "Subjects management HTTP slice requires ISubjectStore. Call AddAccessControlManagement for native EF storage or register your own ISubjectStore.");
        }

        if (options.RolesEnabled && !serviceProbe.IsService(typeof(IRoleCatalog)))
        {
            throw new InvalidOperationException(
                "Roles management HTTP slice requires IRoleCatalog. Call AddAccessControlManagement for native EF storage or register your own IRoleCatalog.");
        }

        if (options.SodEnabled && !serviceProbe.IsService(typeof(ISodConstraintStore)))
        {
            throw new InvalidOperationException(
                "SoD management HTTP slice requires ISodConstraintStore. Call AddAccessControlManagement for native EF storage or register your own ISodConstraintStore.");
        }

        if (options.PolicySetEnabled && !serviceProbe.IsService(typeof(IPolicySetEditor)))
        {
            throw new InvalidOperationException(
                "Policy set management HTTP slice requires IPolicySetEditor. Call AddAccessControlManagement for native EF storage or register your own IPolicySetEditor.");
        }
    }
}

internal sealed record SodConstraintRequest(string Id, string[] MutuallyExclusiveRoles)
{
    public SodConstraint ToConstraint(string? id = null) =>
        new(
            id ?? Id,
            new HashSet<string>(MutuallyExclusiveRoles, StringComparer.Ordinal));
}
