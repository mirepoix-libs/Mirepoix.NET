using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Guards seam and shared DI registrations so provider packages do not silently replace each other.
/// </summary>
public static class AccessControlServiceCollectionGuard
{
    public static void EnsureCanRegisterSeam(
        IServiceCollection services,
        string registrationMethod,
        Type serviceType,
        Type expectedImplementation)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationMethod);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(expectedImplementation);

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != serviceType)
                continue;

            var actual = ResolveImplementationType(descriptor);
            if (actual is not null && expectedImplementation.IsAssignableFrom(actual))
            {
                throw new InvalidOperationException(
                    $"{expectedImplementation.Name} is already registered. " +
                    $"{registrationMethod} was called more than once.");
            }

            var actualName = actual?.Name ?? "an unknown implementation";
            throw new InvalidOperationException(
                $"{serviceType.Name} is already registered as {actualName}. " +
                $"Cannot register {expectedImplementation.Name} via {registrationMethod}. " +
                "Use only one access-control provider package.");
        }
    }

    /// <summary>
    /// Registers a shared service once. Skips if the expected implementation is already present;
    /// throws if a different implementation is registered.
    /// </summary>
    public static void RegisterSharedOnce(
        IServiceCollection services,
        string registrationMethod,
        Type serviceType,
        Type? expectedImplementation,
        Action register)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(registrationMethod);
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(register);

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != serviceType)
                continue;

            var actual = ResolveImplementationType(descriptor);
            if (expectedImplementation is null)
            {
                // Already registered (e.g. options instance) — keep the first.
                return;
            }

            if (actual is not null && expectedImplementation.IsAssignableFrom(actual))
                return;

            var actualName = actual?.Name ?? "an unknown implementation";
            throw new InvalidOperationException(
                $"{serviceType.Name} is already registered as {actualName}. " +
                $"Cannot register {expectedImplementation.Name} via {registrationMethod}.");
        }

        register();
    }

    public static bool IsServiceRegistered(IServiceCollection services, Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceType);
        return services.Any(d => d.ServiceType == serviceType);
    }

    public static Type? ResolveImplementationType(ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.ImplementationType is not null)
            return descriptor.ImplementationType;

        if (descriptor.ImplementationInstance is not null)
            return descriptor.ImplementationInstance.GetType();

        return null;
    }
}
