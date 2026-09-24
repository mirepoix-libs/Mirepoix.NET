using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Opens a DI scope on each <see cref="HydrateAsync"/> and delegates to the registered
/// <see cref="IResourceHydrator"/>, or returns the partial resource when none is registered.
/// </summary>
/// <remarks>
/// Singleton <see cref="IBundleHydrator"/> factories pass this instance into
/// <see cref="CompositeBundleHydrator"/> so they never resolve
/// <see cref="IResourceHydrator"/> from the root provider.
/// Do not register this type as <see cref="IResourceHydrator"/>. The providers HTTP
/// endpoint already resolves that service from the request scope; registering this
/// adapter would open a second scope, or recurse if this type is the registration.
/// </remarks>
internal sealed class ScopeFactoryResourceHydrator : IResourceHydrator
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Captures <paramref name="scopeFactory"/> for per-call resolution.
    /// </summary>
    /// <param name="scopeFactory">Application scope factory. Must outlive this instance.</param>
    public ScopeFactoryResourceHydrator(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Resolves <see cref="IResourceHydrator"/> inside a new scope and delegates.
    /// </summary>
    /// <param name="partial">Resource as supplied by the caller.</param>
    /// <param name="cancellationToken">Cancellation forwarded to the inner hydrator.</param>
    /// <returns>
    /// <paramref name="partial"/> when no <see cref="IResourceHydrator"/> is registered;
    /// otherwise the inner hydrator's result.
    /// </returns>
    public async Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var hydrator = scope.ServiceProvider.GetService<IResourceHydrator>();
        if (hydrator is null)
            return partial;

        return await hydrator.HydrateAsync(partial, cancellationToken).ConfigureAwait(false);
    }
}
