using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Builds the enforcement context attribute catalog.
/// Every read starts with <c>(context, null, time)</c>, then appends <see cref="PublishedEnforcementAttributeList"/>
/// keys in registration order. A key equal to <c>time</c> by ordinal comparison is not added again.
/// Subject and resource rows are never emitted.
/// </summary>
public sealed class PublishedEnforcementAttributeSource : IPublishedEnforcementAttributeSource
{
    private readonly PublishedEnforcementAttributeList _registrations;

    /// <summary>
    /// Reads <paramref name="registrations"/> on each <see cref="List"/> call.
    /// </summary>
    /// <param name="registrations">Context keys from <c>AddAccessControlAttribute</c>.</param>
    public PublishedEnforcementAttributeSource(PublishedEnforcementAttributeList registrations)
    {
        _registrations = registrations;
    }

    /// <summary>
    /// Returns context rows only. <c>time</c> is first. Later keys keep registration order and stay unique by ordinal key.
    /// </summary>
    /// <returns>The catalog <c>GET /access-control/attributes</c> would return.</returns>
    public IReadOnlyList<PublishedAttribute> List()
    {
        var published = new List<PublishedAttribute>
        {
            new(AttributeTarget.Context, null, "time")
        };

        foreach (var key in _registrations.Keys)
        {
            if (string.Equals(key, "time", StringComparison.Ordinal))
                continue;

            published.Add(new PublishedAttribute(AttributeTarget.Context, null, key));
        }

        return published;
    }
}
