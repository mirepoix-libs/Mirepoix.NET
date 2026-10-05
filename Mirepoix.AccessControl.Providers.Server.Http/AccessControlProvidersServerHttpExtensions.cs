using System.Text.Json;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mirepoix.AccessControl.Providers.Server.Http;

/// <summary>
/// Registers and maps providers HTTP hydrate endpoints and the attribute catalog GET.
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
    /// Maps enabled hydrate endpoints and, when enabled, <c>GET</c> <see cref="PublishedAttribute.CatalogPath"/>.
    /// That catalog path is fixed. <see cref="AccessControlProvidersServerHttpOptions.RoutePrefix"/> does not move it.
    /// The attribute catalog slice may be the only slice.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no slice is enabled, when an enabled slice is missing its resolver or
    /// <see cref="IPublishedProviderAttributeSource"/>, or when
    /// <see cref="AccessControlProvidersServerHttpOptions.AuthorizationPolicy"/> is null or white space.
    /// The missing-policy message is "Access-control HTTP routes require an authorization policy."
    /// </exception>
    public static IEndpointRouteBuilder MapAccessControlProviders(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<AccessControlProvidersServerHttpOptions>();
        if (!options.SubjectEnabled && !options.ResourceEnabled && !options.ContextEnabled && !options.AttributeCatalogEnabled)
        {
            throw new InvalidOperationException(
                "Providers HTTP map requires at least one slice. Call AddSubject, AddResource, AddContext, and/or AddAttributeCatalog.");
        }

        var probe = endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        if (options.SubjectEnabled && !probe.IsService(typeof(ISubjectResolver)))
        {
            throw new InvalidOperationException(
                "Providers HTTP subject hydrate requires ISubjectResolver.");
        }

        if (options.ResourceEnabled && !probe.IsService(typeof(IResourceHydrator)))
        {
            throw new InvalidOperationException(
                "Providers HTTP resource hydrate requires IResourceHydrator.");
        }

        if (options.ContextEnabled && !probe.IsService(typeof(IContextResolver)))
        {
            throw new InvalidOperationException(
                "Providers HTTP context hydrate requires IContextResolver.");
        }

        if (options.AttributeCatalogEnabled && !probe.IsService(typeof(IPublishedProviderAttributeSource)))
        {
            throw new InvalidOperationException(
                "Providers HTTP attribute catalog requires IPublishedProviderAttributeSource.");
        }

        if (string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            throw new InvalidOperationException("Access-control HTTP routes require an authorization policy.");
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
                    request.HttpContext,
                    () => body.Value!.ToDomain(),
                    partial => resolver.HydrateAsync(partial, cancellationToken),
                    SubjectDto.FromDomain).ConfigureAwait(false);
            });
        }

        if (options.ResourceEnabled)
        {
            group.MapPost(AccessControlHttpRoutes.ProvidersResourceHydrateRelative, async (
                HttpRequest request,
                IResourceHydrator hydrator,
                CancellationToken cancellationToken) =>
            {
                var body = await ReadBodyAsync<ResourceDto>(request, cancellationToken).ConfigureAwait(false);
                if (body.Problem is not null)
                {
                    return body.Problem;
                }

                return await HydrateAsync(
                    request.HttpContext,
                    () => body.Value!.ToDomain(),
                    partial => hydrator.HydrateAsync(partial, cancellationToken),
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
                    request.HttpContext,
                    () => body.Value!.ToDomain(),
                    partial => resolver.HydrateAsync(partial, cancellationToken),
                    AccessContextDto.FromDomain).ConfigureAwait(false);
            });
        }

        if (options.AttributeCatalogEnabled)
        {
            var catalog = endpoints.MapGet(
                PublishedAttribute.CatalogPath,
                (IPublishedProviderAttributeSource source) =>
                    Results.Json(source.List(), AccessControlHttpJson.DefaultOptions));
            if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
                catalog.RequireAuthorization(options.AuthorizationPolicy);
        }

        return endpoints;
    }

    private static async Task<IResult> HydrateAsync<TDomain, TDto>(
        HttpContext http,
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
            LogHydrateFailure(http, exception);
            return Results.Problem("The hydrate request is invalid.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var hydrated = await hydrate(partial).ConfigureAwait(false);
            return Results.Json(toDto(hydrated), AccessControlHttpJson.DefaultOptions);
        }
        catch (KeyNotFoundException exception)
        {
            LogHydrateFailure(http, exception);
            return Results.Problem("Not found.", statusCode: StatusCodes.Status404NotFound);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogHydrateFailure(http, exception);
            return Results.Problem("Hydration failed.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static void LogHydrateFailure(HttpContext http, Exception exception)
    {
        var logger = http.RequestServices.GetService<ILoggerFactory>()
            ?.CreateLogger("Mirepoix.AccessControl.Providers.Server.Http");
        logger?.LogError(exception, "Access-control hydrate failed.");
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
            var logger = request.HttpContext.RequestServices.GetService<ILoggerFactory>()
                ?.CreateLogger("Mirepoix.AccessControl.Providers.Server.Http");
            logger?.LogError(exception, "Access-control hydrate failed.");
            return (null, Results.Problem("The hydrate request is invalid.", statusCode: StatusCodes.Status400BadRequest));
        }

        if (body is null)
        {
            return (null, Results.Problem("Request body is required.", statusCode: StatusCodes.Status400BadRequest));
        }

        return (body, null);
    }
}
