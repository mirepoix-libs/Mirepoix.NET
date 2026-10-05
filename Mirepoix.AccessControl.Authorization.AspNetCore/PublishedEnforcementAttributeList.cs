using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Holds context attribute keys registered with <c>AddAccessControlAttribute</c>.
/// Built-in <c>time</c> is not stored here; <see cref="PublishedEnforcementAttributeSource"/> adds it on each read.
/// </summary>
public sealed class PublishedEnforcementAttributeList
{
    private readonly List<string> _keys = new();

    /// <summary>
    /// Returns the keys stored so far, in registration order. Duplicate keys were ignored at add time.
    /// </summary>
    internal IReadOnlyList<string> Keys => _keys;

    /// <summary>
    /// Returns the singleton list already on <paramref name="services"/>, or adds a new one.
    /// A type-only registration is replaced so later adds mutate the instance DI will resolve.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <returns>The list instance stored as a singleton.</returns>
    internal static PublishedEnforcementAttributeList GetOrAdd(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(PublishedEnforcementAttributeList))
                continue;

            if (descriptor.ImplementationInstance is PublishedEnforcementAttributeList existing)
                return existing;

            services.RemoveAt(i);
        }

        var created = new PublishedEnforcementAttributeList();
        services.AddSingleton(created);
        return created;
    }

    /// <summary>
    /// Appends <paramref name="key"/> when no stored key equals it by ordinal comparison.
    /// Callers reject null and blank keys before this runs. A stored <c>time</c> is still one row at list time.
    /// </summary>
    /// <param name="key">Context attribute key.</param>
    internal void Add(string key)
    {
        foreach (var existing in _keys)
        {
            if (string.Equals(existing, key, StringComparison.Ordinal))
                return;
        }

        _keys.Add(key);
    }
}
