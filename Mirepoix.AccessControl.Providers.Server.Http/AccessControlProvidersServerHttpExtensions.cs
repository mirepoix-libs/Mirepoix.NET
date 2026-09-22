using System.Text.Json;
using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers.Server.Http;

/// <summary>
/// Registers and maps providers HTTP hydrate endpoints (remote PIP surface).
/// </summary>
public static class AccessControlProvidersServerHttpExtensions
{
    /// <summary>Registers providers HTTP server options.</summary>
    public static IServiceCollection AddAccessControlProvidersServerHttp(
        this IServiceCollection services,
        Action<AccessControlProvidersServerHttpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new AccessControlProvidersServerHttpOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    /// Maps enabled PIP hydrate endpoints under the configured route prefix.
    /// </summary>
    public static IEndpointRouteBuilder MapAccessControlProviders(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<AccessControlProvidersServerHttpOptions>();
        if (!options.SubjectEnabled && !options.ResourceEnabled && !options.ContextEnabled)
        {
            throw new InvalidOperationException(
                "Providers HTTP map requires at least one slice. Call AddSubject, AddResource, and/or AddContext.");
        }

        var probe = endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        if (options.SubjectEnabled && !probe.IsService(typeof(ISubjectResolver)))
        {
            throw new InvalidOperationException(
                "Providers HTTP subject hydrate requires ISubjectResolver.");
        }

        if (options.ResourceEnabled && !probe.IsService(typeof(IResourceResolver)))
        {
            throw new InvalidOperationException(
                "Providers HTTP resource hydrate requires IResourceResolver.");
        }

        if (options.ContextEnabled && !probe.IsService(typeof(IContextResolver)))
        {
            throw new InvalidOperationException(
                "Providers HTTP context hydrate requires IContextResolver.");
        }

        var group = endpoints.MapGroup(options.RoutePrefix);
        if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        if (options.SubjectEnabled)
        {
            group.MapPost(AccessControlHttpRoutes.ProvidersSubjectHydrateRelative, async (
                HttpRequest request,
                ISubjectResolver resolver,
                CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync<SubjectDto>(request, cancellationToken).ConfigureAwait(false);
                if (body.Problem is not null)
                {
                    return body.Problem;
                }

                return await HydrateAsync(
                    () => body.Value!.ToDomain(),
                    partial => resolver.HydrateAsync(partial, cancellationToken),
                    SubjectDto.FromDomain).ConfigureAwait(false);
            });
        }

        if (options.ResourceEnabled)
        {
            group.MapPost(AccessControlHttpRoutes.ProvidersResourceHydrateRelative, async (
                HttpRequest request,
                IResourceResolver resolver,
                CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync<ResourceDto>(request, cancellationToken).ConfigureAwait(false);
                if (body.Problem is not null)
                {
                    return body.Problem;
                }

                return await HydrateAsync(
                    () => body.Value!.ToDomain(),
                    partial => resolver.HydrateAsync(partial, cancellationToken),
                    ResourceDto.FromDomain).ConfigureAwait(false);
            });
        }

        if (options.ContextEnabled)
        {
            group.MapPost(AccessControlHttpRoutes.ProvidersContextHydrateRelative, async (
                HttpRequest request,
                IContextResolver resolver,
                CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync<AccessContextDto>(request, cancellationToken).ConfigureAwait(false);
                if (body.Problem is not null)
                {
                    return body.Problem;
                }

                return await HydrateAsync(
                    () => body.Value!.ToDomain(),
                    partial => resolver.HydrateAsync(partial, cancellationToken),
                    AccessContextDto.FromDomain).ConfigureAwait(false);
            });
        }

        return endpoints;
    }

    private static async Task<IResult> HydrateAsync<TDomain, TDto>(
        Func<TDomain> toDomain,
        Func<TDomain, Task<TDomain>> hydrate,
        Func<TDomain, TDto> toDto)
    {
        TDomain partial;
        try
        {
            partial = toDomain();
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var hydrated = await hydrate(partial).ConfigureAwait(false);
            return Results.Json(toDto(hydrated), AccessControlHttpJson.DefaultOptions);
        }
        catch (KeyNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (Exception exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<(T? Value, IResult? Problem)> ReadBodyAsync<T>(
        HttpRequest request,
        CancellationToken cancellationToken)
        where T : class
    {
        T? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<T>(
                request.Body,
                AccessControlHttpJson.DefaultOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            return (null, Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest));
        }

        if (body is null)
        {
            return (null, Results.Problem("Request body is required.", statusCode: StatusCodes.Status400BadRequest));
        }

        return (body, null);
    }
}
