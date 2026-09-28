using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Holds policies appended on read and stripped from replace. Not stored.
/// </summary>
public sealed class PinnedPolicyCollection
{
    internal List<PolicyModel> Policies { get; } = [];

    /// <summary>
    /// Appends <paramref name="policy"/> in evaluation order.
    /// </summary>
    /// <param name="policy">Pin copied into the effective set on read.</param>
    /// <exception cref="ArgumentException">Thrown when another pin already uses the same id (ordinal).</exception>
    public void Add(PolicyModel policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        foreach (var existing in Policies)
        {
            if (string.Equals(existing.Id, policy.Id, StringComparison.Ordinal))
                throw new ArgumentException($"Pinned policy '{policy.Id}' is already registered.");
        }

        Policies.Add(policy);
    }
}

/// <summary>
/// Decorates <see cref="IPolicySource"/> and <see cref="IPolicySetEditor"/> with pinned policies.
/// </summary>
public static class PinnedPolicyServiceCollectionExtensions
{
    /// <summary>
    /// Wraps the last registered source and editor so <paramref name="configure"/> pins are not stored.
    /// </summary>
    /// <param name="services">Collection that already contains <see cref="IPolicySource"/> and <see cref="IPolicySetEditor"/>.</param>
    /// <param name="configure">Adds at least one pin. Called once during registration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// Removes each last matching descriptor and adds a factory of the same lifetime.
    /// The factory uses the descriptor instance, then its factory, then <see cref="ActivatorUtilities.CreateInstance"/>.
    /// Also registers <see cref="PinnedPolicySource"/> as itself so a second call can see it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown with message "At least one pinned policy is required." when the callback adds nothing.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown with message "Access-control pinned policies are already registered." when <see cref="PinnedPolicySource"/> is already registered.
    /// Thrown with message "Pinned policies require IPolicySource and IPolicySetEditor." when either service is missing.
    /// </exception>
    public static IServiceCollection AddAccessControlPinnedPolicies(
        this IServiceCollection services,
        Action<PinnedPolicyCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var pins = new PinnedPolicyCollection();
        configure(pins);
        if (pins.Policies.Count == 0)
            throw new ArgumentException("At least one pinned policy is required.");

        if (services.Any(descriptor => descriptor.ServiceType == typeof(PinnedPolicySource)))
            throw new InvalidOperationException("Access-control pinned policies are already registered.");

        var source = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IPolicySource));
        var editor = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IPolicySetEditor));
        if (source is null || editor is null)
            throw new InvalidOperationException("Pinned policies require IPolicySource and IPolicySetEditor.");

        IReadOnlyList<PolicyModel> registered = pins.Policies.ToArray();

        services.Remove(source);
        services.Add(new ServiceDescriptor(
            typeof(IPolicySource),
            provider => new PinnedPolicySource(Create<IPolicySource>(provider, source), registered),
            source.Lifetime));
        services.Add(new ServiceDescriptor(
            typeof(PinnedPolicySource),
            provider => (PinnedPolicySource)provider.GetRequiredService<IPolicySource>(),
            source.Lifetime));

        services.Remove(editor);
        services.Add(new ServiceDescriptor(
            typeof(IPolicySetEditor),
            provider => new PinnedPolicySetEditor(Create<IPolicySetEditor>(provider, editor), registered),
            editor.Lifetime));

        return services;
    }

    private static T Create<T>(IServiceProvider provider, ServiceDescriptor descriptor)
        where T : class
    {
        if (descriptor.ImplementationInstance is T instance)
            return instance;

        if (descriptor.ImplementationFactory is not null)
            return (T)descriptor.ImplementationFactory(provider);

        return (T)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }
}
