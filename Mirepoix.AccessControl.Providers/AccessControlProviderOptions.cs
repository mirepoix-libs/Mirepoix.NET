using Mirepoix.AccessControl.Policy;

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

    /// <summary>Holds explicit subject and resource keys added with <c>AddAttribute</c>.</summary>
    internal PublishedProviderAttributeList ExplicitAttributes { get; } = new();

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

    /// <summary>
    /// Publishes an extra subject or resource attribute that maps do not already export.
    /// A repeated <c>(target, type, key)</c> is ignored. The catalog list also drops <c>time</c>.
    /// </summary>
    /// <param name="target">Subject or resource. Context is rejected.</param>
    /// <param name="key">Non-blank bundle attribute key.</param>
    /// <param name="type">
    /// Catalog type. Null on subject becomes <c>subject</c>. Resource requires a non-blank type.
    /// </param>
    /// <returns>This options instance for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="key"/> is blank, <paramref name="target"/> is context,
    /// a provided subject type is blank, or resource type is missing.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="target"/> is not a known value.</exception>
    public TSelf AddAttribute(AttributeTarget target, string key, string? type = null)
    {
        ExplicitAttributes.Add(target, key, type);
        return (TSelf)this;
    }
}

/// <summary>Options for agnostic access-control provider registration (resource maps only).</summary>
public sealed class AgnosticAccessControlProviderOptions
    : AccessControlProviderOptions<AgnosticAccessControlProviderOptions>;
