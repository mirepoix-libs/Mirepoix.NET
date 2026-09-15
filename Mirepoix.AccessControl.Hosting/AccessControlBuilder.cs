using Mirepoix.AccessControl.Evaluation;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirepoix.AccessControl.Hosting;

public sealed class AccessControlBuilder
{
    private readonly IServiceCollection _services;
    private bool _policySourceRegistered;

    internal AccessControlBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public AccessControlBuilder UseMemoryPolicySet(PolicySet policySet)
    {
        ArgumentNullException.ThrowIfNull(policySet);
        _services.RemoveAll<IPolicySource>();
        _services.AddSingleton<IPolicySource>(new MemoryPolicySource(policySet));
        _policySourceRegistered = true;
        return this;
    }

    public AccessControlBuilder UsePolicySource<TSource>()
        where TSource : class, IPolicySource
    {
        _services.RemoveAll<IPolicySource>();
        _services.AddSingleton<IPolicySource, TSource>();
        _policySourceRegistered = true;
        return this;
    }

    public AccessControlBuilder UsePolicySource(IPolicySource policySource)
    {
        ArgumentNullException.ThrowIfNull(policySource);
        _services.RemoveAll<IPolicySource>();
        _services.AddSingleton(policySource);
        _policySourceRegistered = true;
        return this;
    }

    public AccessControlBuilder UseHydrator(IBundleHydrator hydrator)
    {
        ArgumentNullException.ThrowIfNull(hydrator);
        _services.RemoveAll<IBundleHydrator>();
        _services.AddSingleton(hydrator);
        return this;
    }

    public AccessControlBuilder UseCompositeHydrator(
        ISubjectResolver? subject = null,
        IResourceResolver? resource = null,
        IContextResolver? context = null)
    {
        return UseHydrator(new CompositeBundleHydrator(subject, resource, context));
    }

    public AccessControlBuilder UseCombinationStrategy(ICombinationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _services.RemoveAll<ICombinationStrategy>();
        _services.AddSingleton(strategy);
        return this;
    }

    public AccessControlBuilder UseClaimsPrincipalMapper<TMapper>()
        where TMapper : class, IClaimsPrincipalMapper
    {
        _services.RemoveAll<IClaimsPrincipalMapper>();
        _services.AddSingleton<IClaimsPrincipalMapper, TMapper>();
        return this;
    }

    internal void Complete()
    {
        if (!_policySourceRegistered
            && !_services.Any(d => d.ServiceType == typeof(IPolicySource)))
        {
            throw new InvalidOperationException(
                "AccessControl requires an IPolicySource. Call UseMemoryPolicySet, UsePolicySource, or register one first (e.g. AddAccessControlProviders).");
        }

        _services.TryAddSingleton<IClaimsPrincipalMapper, DefaultClaimsPrincipalMapper>();
        _services.TryAddSingleton<IBundleHydrator>(_ => new CompositeBundleHydrator());
        _services.TryAddSingleton<ICombinationStrategy, DenyOverridesStrategy>();

        _services.TryAddSingleton<IAccessChecker>(sp =>
        {
            var source = sp.GetRequiredService<IPolicySource>();
            var hydrator = sp.GetRequiredService<IBundleHydrator>();
            var strategy = sp.GetService<ICombinationStrategy>();
            return new LocalAccessChecker(source, hydrator, strategy);
        });

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
}
