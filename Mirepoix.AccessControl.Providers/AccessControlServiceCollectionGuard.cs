using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Guards seam and shared DI registrations so provider packages do not silently replace each other.
/// Used by durable provider registration helpers when installing Engine seams (policy source, resolvers)
/// and shared options/migrator services.
/// </summary>
public static class AccessControlServiceCollectionGuard
{
    /// <summary>
    /// Ensures <paramref name="serviceType"/> is not already registered. Throws if it is.
    /// Distinguishes "same implementation registered twice" from "a different implementation already owns the seam".
    /// </summary>
    /// <param name="services">DI collection under construction.</param>
    /// <param name="registrationMethod">Caller method name included in exception text (for diagnostics).</param>
    /// <param name="serviceType">Seam service type (e.g. <c>IPolicySource</c>).</param>
    /// <param name="expectedImplementation">Implementation type this call intends to register.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the seam is already registered (duplicate same type, or a conflicting implementation).
    /// </exception>
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
    /// throws if a different implementation is registered. When <paramref name="expectedImplementation"/>
    /// is null, any existing registration of <paramref name="serviceType"/> causes a skip (keep first).
    /// </summary>
    /// <param name="services">DI collection under construction.</param>
    /// <param name="registrationMethod">Caller method name included in exception text.</param>
    /// <param name="serviceType">Shared service type (e.g. options).</param>
    /// <param name="expectedImplementation">
    /// Implementation that is allowed to already exist (idempotent). Null means "any registration counts as present".
    /// </param>
    /// <param name="register">Action that performs the actual <c>Add*</c> when the service is absent.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a different implementation already owns <paramref name="serviceType"/>.
    /// </exception>
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
                // Already registered (e.g. options instance): keep the first.
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

    /// <summary>
    /// Returns whether any descriptor for <paramref name="serviceType"/> is present.
    /// </summary>
    /// <param name="services">DI collection to inspect.</param>
    /// <param name="serviceType">Service type to look for.</param>
    public static bool IsServiceRegistered(IServiceCollection services, Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceType);
        return services.Any(d => d.ServiceType == serviceType);
    }

    /// <summary>
    /// Resolves the concrete implementation type from a descriptor when possible.
    /// Prefers <see cref="ServiceDescriptor.ImplementationType"/>, then the runtime type of
    /// <see cref="ServiceDescriptor.ImplementationInstance"/>. Returns null for factory-only descriptors
    /// (implementation type cannot be known statically).
    /// </summary>
    /// <param name="descriptor">Service descriptor to inspect.</param>
    /// <returns>Known implementation type, or null when only a factory is registered.</returns>
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
