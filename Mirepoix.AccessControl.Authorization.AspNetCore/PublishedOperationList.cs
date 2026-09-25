using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Holds concrete operation strings registered with <c>AddAccessControlOperation</c>.
/// Endpoint metadata is not stored here; <see cref="PublishedOperationSource"/> reads that at list time.
/// </summary>
public sealed class PublishedOperationList
{
    private readonly List<string> _operations = new();

    /// <summary>
    /// Returns the concrete strings stored so far, in registration order.
    /// </summary>
    internal IReadOnlyList<string> Operations => _operations;

    /// <summary>
    /// Returns the singleton list already on <paramref name="services"/>, or adds a new one.
    /// A type-only registration is replaced so later adds mutate the instance DI will resolve.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <returns>The list instance stored as a singleton.</returns>
    internal static PublishedOperationList GetOrAdd(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(PublishedOperationList))
                continue;

            if (descriptor.ImplementationInstance is PublishedOperationList existing)
                return existing;

            services.RemoveAt(i);
        }

        var created = new PublishedOperationList();
        services.AddSingleton(created);
        return created;
    }

    /// <summary>
    /// Appends <paramref name="operation"/> without validating it.
    /// Callers reject null, blank, and <c>*</c> segments before this runs.
    /// </summary>
    /// <param name="operation">Concrete operation string.</param>
    internal void Add(string operation) => _operations.Add(operation);
}
