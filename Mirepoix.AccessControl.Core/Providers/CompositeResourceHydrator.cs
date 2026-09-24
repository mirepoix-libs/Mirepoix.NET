namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Dispatches resource hydration to the resolver registered for the partial resource type.
/// </summary>
public sealed class CompositeResourceHydrator : IResourceHydrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<string, ResourceResolverRegistration> _registrations;

    /// <summary>
    /// Creates a dispatcher over resource resolver registrations.
    /// </summary>
    /// <param name="serviceProvider">Service provider used by registration delegates.</param>
    /// <param name="registrations">Resource resolver registrations keyed by type.</param>
    /// <exception cref="InvalidOperationException">Thrown when a type is registered more than once.</exception>
    public CompositeResourceHydrator(
        IServiceProvider serviceProvider,
        IEnumerable<ResourceResolverRegistration> registrations)
    {
        _serviceProvider = serviceProvider;

        var registrationsByType = new Dictionary<string, ResourceResolverRegistration>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            if (!registrationsByType.TryAdd(registration.Type, registration))
                throw new InvalidOperationException(
                    $"Resource type '{registration.Type}' has more than one resolver.");
        }

        _registrations = registrationsByType;
    }

    /// <inheritdoc />
    public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(partial.Type) && string.IsNullOrEmpty(partial.Id))
            return Task.FromResult(partial);

        if (string.IsNullOrEmpty(partial.Type))
            throw new InvalidOperationException("A resource id requires a resource type.");

        if (!_registrations.TryGetValue(partial.Type, out var registration))
            throw new InvalidOperationException(
                $"No resource resolver is registered for type '{partial.Type}'.");

        return registration.Invoke(_serviceProvider, partial, cancellationToken);
    }
}
