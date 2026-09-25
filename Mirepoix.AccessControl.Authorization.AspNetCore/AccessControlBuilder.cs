using Mirepoix.AccessControl.Evaluation;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Configures Hosting DI: policy source, hydrator, combination strategy, claims mapper, checker, and PEPs.
/// Completion fails fast when neither <see cref="IPolicySource"/> nor <see cref="IAccessChecker"/> is registered.
/// </summary>
public sealed class AccessControlBuilder
{
    private readonly IServiceCollection _services;
    private bool _policySourceRegistered;

    /// <summary>
    /// Creates a builder over <paramref name="services"/>. Prefer <see cref="AccessControlServiceCollectionExtensions.AddAccessControl"/>.
    /// </summary>
    internal AccessControlBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Registers an in-memory <see cref="MemoryPolicySource"/> seeded with <paramref name="policySet"/>,
    /// replacing any existing <see cref="IPolicySource"/>.
    /// </summary>
    /// <param name="policySet">Policy set served for the process lifetime of the singleton source.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policySet"/> is null.</exception>
    public AccessControlBuilder UseMemoryPolicySet(PolicySet policySet)
    {
        ArgumentNullException.ThrowIfNull(policySet);
        _services.RemoveAll<IPolicySource>();
        _services.AddSingleton<IPolicySource>(new MemoryPolicySource(policySet));
        _policySourceRegistered = true;
        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TSource"/> as the singleton <see cref="IPolicySource"/>,
    /// replacing any existing registration.
    /// </summary>
    /// <typeparam name="TSource">Concrete <see cref="IPolicySource"/> implementation.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public AccessControlBuilder UsePolicySource<TSource>()
        where TSource : class, IPolicySource
    {
        _services.RemoveAll<IPolicySource>();
        _services.AddSingleton<IPolicySource, TSource>();
        _policySourceRegistered = true;
        return this;
    }

    /// <summary>
    /// Registers <paramref name="policySource"/> as the singleton <see cref="IPolicySource"/>,
    /// replacing any existing registration.
    /// </summary>
    /// <param name="policySource">Existing source instance.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policySource"/> is null.</exception>
    public AccessControlBuilder UsePolicySource(IPolicySource policySource)
    {
        ArgumentNullException.ThrowIfNull(policySource);
        _services.RemoveAll<IPolicySource>();
        _services.AddSingleton(policySource);
        _policySourceRegistered = true;
        return this;
    }

    /// <summary>
    /// Registers <paramref name="hydrator"/> as the singleton <see cref="IBundleHydrator"/>,
    /// replacing any existing registration.
    /// </summary>
    /// <param name="hydrator">Hydrator used by <see cref="LocalAccessChecker"/>.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hydrator"/> is null.</exception>
    public AccessControlBuilder UseHydrator(IBundleHydrator hydrator)
    {
        ArgumentNullException.ThrowIfNull(hydrator);
        _services.RemoveAll<IBundleHydrator>();
        _services.AddSingleton(hydrator);
        return this;
    }

    /// <summary>
    /// Registers a <see cref="CompositeBundleHydrator"/> with the given optional resolvers.
    /// Omitted resolvers become no-ops inside the composite.
    /// </summary>
    /// <param name="subject">Optional subject resolver.</param>
    /// <param name="resource">Optional resource hydrator.</param>
    /// <param name="context">Optional context resolver.</param>
    /// <returns>This builder for chaining.</returns>
    /// <remarks>
    /// The caller owns the lifetime of any instance passed here. Do not pass a scoped
    /// instance. This method registers the composite as a singleton and would capture
    /// that instance for the process lifetime. Register the hydrator in DI and use
    /// composite wiring so each hydrate call resolves it from its own scope.
    /// </remarks>
    public AccessControlBuilder UseCompositeHydrator(
        ISubjectResolver? subject = null,
        IResourceHydrator? resource = null,
        IContextResolver? context = null)
    {
        return UseHydrator(new CompositeBundleHydrator(subject, resource, context));
    }

    /// <summary>
    /// Registers <paramref name="strategy"/> as the singleton <see cref="ICombinationStrategy"/>,
    /// replacing any existing registration.
    /// </summary>
    /// <param name="strategy">Combination strategy for the kernel.</param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strategy"/> is null.</exception>
    public AccessControlBuilder UseCombinationStrategy(ICombinationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _services.RemoveAll<ICombinationStrategy>();
        _services.AddSingleton(strategy);
        return this;
    }

    /// <summary>
    /// Registers <typeparamref name="TMapper"/> as the singleton <see cref="IClaimsPrincipalMapper"/>,
    /// replacing any existing registration.
    /// </summary>
    /// <typeparam name="TMapper">Custom mapper implementation.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public AccessControlBuilder UseClaimsPrincipalMapper<TMapper>()
        where TMapper : class, IClaimsPrincipalMapper
    {
        _services.RemoveAll<IClaimsPrincipalMapper>();
        _services.AddSingleton<IClaimsPrincipalMapper, TMapper>();
        return this;
    }

    /// <summary>
    /// Completes DI: validates <see cref="IPolicySource"/> or pre-registered <see cref="IAccessChecker"/>, then TryAdds
    /// defaults for mapper, hydrator, combination strategy, <see cref="LocalAccessChecker"/>, authorization handler,
    /// policy, and scoped <see cref="AccessEndpointFilter"/>. Replaces <see cref="IAccessChecker"/> with
    /// <see cref="PublishedOperationAccessChecker"/> around the previous registration, including a remote checker.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither this builder nor the service collection has an <see cref="IPolicySource"/> or
    /// <see cref="IAccessChecker"/>.
    /// </exception>
    /// <remarks>
    /// Defaults when absent: <see cref="DefaultClaimsPrincipalMapper"/>,
    /// a <see cref="CompositeBundleHydrator"/> that resolves subject and context from DI
    /// and resolves <see cref="IResourceHydrator"/> from a new scope on each call,
    /// <see cref="DenyOverridesStrategy"/>, and policy name <see cref="AccessControlOptions.PolicyName"/>.
    /// </remarks>
    internal void Complete()
    {
        var checkerAlreadyRegistered = _services.Any(d => d.ServiceType == typeof(IAccessChecker));
        if (!_policySourceRegistered
            && !_services.Any(d => d.ServiceType == typeof(IPolicySource))
            && !checkerAlreadyRegistered)
        {
            throw new InvalidOperationException(
                "AccessControl requires an IPolicySource. Call UseMemoryPolicySet, UsePolicySource, or register one first (e.g. AddAccessControlProviders).");
        }

        _services.TryAddSingleton<IClaimsPrincipalMapper, DefaultClaimsPrincipalMapper>();
        _services.TryAddSingleton<IBundleHydrator>(sp =>
            new CompositeBundleHydrator(
                sp.GetService<ISubjectResolver>(),
                new ScopeFactoryResourceHydrator(sp.GetRequiredService<IServiceScopeFactory>()),
                sp.GetService<IContextResolver>()));
        _services.TryAddSingleton<ICombinationStrategy, DenyOverridesStrategy>();

        _services.TryAddSingleton<IAccessChecker>(sp =>
        {
            var source = sp.GetRequiredService<IPolicySource>();
            var hydrator = sp.GetRequiredService<IBundleHydrator>();
            var strategy = sp.GetService<ICombinationStrategy>();
            return new LocalAccessChecker(source, hydrator, strategy);
        });

        PublishedOperationList.GetOrAdd(_services);
        _services.TryAddSingleton<PublishedOperationSource>();
        WrapAccessChecker();

        _services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAuthorizationHandler, AccessAuthorizationHandler>());

        _services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AccessControlOptions.PolicyName,
                policy => policy.AddRequirements(new AccessRequirement()));
        });

        _services.TryAddScoped<AccessEndpointFilter>();
    }

    /// <summary>
    /// Replaces the last <see cref="IAccessChecker"/> registration with <see cref="PublishedOperationAccessChecker"/>.
    /// The previous instance, factory, or type is invoked as the inner checker, including a remote checker
    /// that was registered before completion.
    /// </summary>
    private void WrapAccessChecker()
    {
        ServiceDescriptor? previous = null;
        for (var i = _services.Count - 1; i >= 0; i--)
        {
            if (_services[i].ServiceType != typeof(IAccessChecker))
                continue;

            previous = _services[i];
            _services.RemoveAt(i);
            break;
        }

        if (previous is null)
            return;

        var captured = previous;
        _services.Add(ServiceDescriptor.Describe(
            typeof(IAccessChecker),
            sp => new PublishedOperationAccessChecker(
                CreateInnerChecker(sp, captured),
                sp.GetRequiredService<PublishedOperationSource>()),
            captured.Lifetime));
    }

    /// <summary>
    /// Builds the checker the descriptor registered before it was wrapped.
    /// </summary>
    /// <param name="services">Provider used for factory and type activations.</param>
    /// <param name="descriptor">Registration captured before replacement.</param>
    /// <returns>The inner <see cref="IAccessChecker"/>.</returns>
    private static IAccessChecker CreateInnerChecker(IServiceProvider services, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IAccessChecker instance)
            return instance;

        if (descriptor.ImplementationFactory is not null)
            return (IAccessChecker)descriptor.ImplementationFactory(services);

        return (IAccessChecker)ActivatorUtilities.CreateInstance(
            services,
            descriptor.ImplementationType!);
    }
}
