using System.Text.Json;
using Mirepoix.AccessControl.Protocol.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Engine.Server.Http;

/// <summary>
/// Registers and maps the engine HTTP check endpoint (PDP remote-eval surface).
/// </summary>
public static class AccessControlEngineServerHttpExtensions
{
    /// <summary>Registers engine HTTP server options.</summary>
    public static IServiceCollection AddAccessControlEngineServerHttp(
        this IServiceCollection services,
        Action<AccessControlEngineServerHttpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new AccessControlEngineServerHttpOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        return services;
    }

    /// <summary>Maps <c>POST {prefix}/check</c> to the registered <see cref="IAccessChecker"/>.</summary>
    public static IEndpointRouteBuilder MapAccessControlEngine(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<AccessControlEngineServerHttpOptions>();
        var probe = endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>();
        if (!probe.IsService(typeof(IAccessChecker)))
        {
            throw new InvalidOperationException(
                "Engine HTTP check requires IAccessChecker. Register LocalAccessChecker via AddAccessControl or register your own.");
        }

        var group = endpoints.MapGroup(options.RoutePrefix);
        if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(options.AuthorizationPolicy);
        }

        group.MapPost(AccessControlHttpRoutes.CheckRelative, async (
            HttpRequest request,
            IAccessChecker checker,
            CancellationToken cancellationToken) =>
        {
            AuthorizationRequestDto? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<AuthorizationRequestDto>(
                    request.Body,
                    AccessControlHttpJson.DefaultOptions,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            if (body is null)
            {
                return Results.Problem("Request body is required.", statusCode: StatusCodes.Status400BadRequest);
            }

            AuthorizationRequest domainRequest;
            try
            {
                domainRequest = body.ToDomain();
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            var decision = await checker.CheckAsync(domainRequest, cancellationToken).ConfigureAwait(false);
            return Results.Json(AccessDecisionDto.FromDomain(decision), AccessControlHttpJson.DefaultOptions);
        });

        return endpoints;
    }
}
