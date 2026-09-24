namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Non-generic base for access-control provider options shared by EF, SqlServer, and agnostic hosts.
/// </summary>
public abstract class AccessControlProviderOptions
{
    /// <summary>Gets subject entity mapping configuration.</summary>
    public SubjectMappingOptions SubjectMapping { get; } = new();

    /// <summary>Gets resource entity mapping configuration.</summary>
    public ResourceMappingOptions ResourceMapping { get; } = new();

    /// <summary>Reports whether a policy source was requested via fluent configuration.</summary>
    public bool PolicySourceRequested { get; protected set; }

    /// <summary>Reports whether a subject resolver was requested via fluent configuration.</summary>
    public bool SubjectResolverRequested { get; protected set; }

    /// <summary>Reports whether a resource hydrator was requested via fluent configuration.</summary>
    public bool ResourceHydratorRequested { get; protected set; }

    /// <summary>Reports whether resource mapping implied a hydrator is needed.</summary>
    public bool ResourceHydratorImplied { get; protected set; }

    /// <summary>
    /// Set by registration when the current call requested a policy source.
    /// </summary>
    internal bool PolicySourceCallRequested { get; set; }

    /// <summary>
    /// Set by registration when the current call requested a subject resolver.
    /// </summary>
    internal bool SubjectResolverCallRequested { get; set; }

    /// <summary>Reports whether resource maps were applied to DI.</summary>
    public bool ResourceMapsApplied { get; private set; }

    /// <summary>
    /// Reports whether a resource hydrator is needed from explicit request, implied mapping, or existing maps.
    /// </summary>
    public bool NeedsResourceHydrator =>
        ResourceHydratorRequested || ResourceHydratorImplied || ResourceMapping.HasMaps;

    /// <summary>Marks resource maps as registered in DI (idempotent).</summary>
    internal void MarkResourceMapsApplied() => ResourceMapsApplied = true;
}

/// <summary>
/// CRTP base for access-control provider options shared by EF, SqlServer, and agnostic hosts.
/// </summary>
/// <typeparam name="TSelf">The concrete options type for fluent chaining.</typeparam>
public abstract class AccessControlProviderOptions<TSelf> : AccessControlProviderOptions
    where TSelf : AccessControlProviderOptions<TSelf>
{
    /// <summary>Configures a subject entity map.</summary>
    /// <typeparam name="T">CLR subject type.</typeparam>
    /// <param name="configure">Builder configuration.</param>
    /// <returns>This options instance for chaining.</returns>
    public TSelf MapSubject<T>(Action<SubjectEntityMapBuilder<T>> configure)
        where T : class
    {
        SubjectMapping.MapEntity(configure);
        return (TSelf)this;
    }

    /// <summary>Configures a resource entity map and implies a resource hydrator is needed.</summary>
    /// <typeparam name="T">CLR resource type.</typeparam>
    /// <param name="configure">Builder configuration.</param>
    /// <returns>This options instance for chaining.</returns>
    public TSelf MapResource<T>(Action<ResourceEntityMapBuilder<T>> configure)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ResourceEntityMapBuilder<T>();
        configure(builder);
        ResourceMapping.Add(builder.Build());
        ResourceHydratorImplied = true;
        return (TSelf)this;
    }

    /// <summary>Requests a policy source registration.</summary>
    /// <returns>This options instance for chaining.</returns>
    public TSelf AddPolicySource()
    {
        PolicySourceRequested = true;
        return (TSelf)this;
    }

    /// <summary>Requests a subject resolver registration.</summary>
    /// <returns>This options instance for chaining.</returns>
    public TSelf AddSubjectResolver()
    {
        SubjectResolverRequested = true;
        return (TSelf)this;
    }

    /// <summary>Requests a resource hydrator registration.</summary>
    /// <returns>This options instance for chaining.</returns>
    public TSelf AddResourceHydrator()
    {
        ResourceHydratorRequested = true;
        return (TSelf)this;
    }
}

/// <summary>Options for agnostic access-control provider registration (resource maps only).</summary>
public sealed class AgnosticAccessControlProviderOptions
    : AccessControlProviderOptions<AgnosticAccessControlProviderOptions>;
