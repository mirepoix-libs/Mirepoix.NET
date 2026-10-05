using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Builds the live subject/resource catalog from configured maps plus explicit <c>AddAttribute</c> keys.
/// Omits context rows and the key <c>time</c>. Duplicate <c>(target, type, key)</c> rows collapse.
/// </summary>
public sealed class PublishedProviderAttributeSource : IPublishedProviderAttributeSource
{
    private readonly AccessControlProviderOptions _options;

    /// <summary>
    /// Reads maps and extras from <paramref name="options"/> on each <see cref="List"/> call.
    /// </summary>
    /// <param name="options">Provider options that own the maps and explicit catalog.</param>
    public PublishedProviderAttributeSource(AccessControlProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Unions resource maps, subject maps, then explicit extras.
    /// Resource type comes from the map. Subject type is <see cref="SubjectEntityMap.FixedTypeValue"/>
    /// when disposition writes that value as an attribute; otherwise <c>subject</c>.
    /// </summary>
    /// <returns>Subject and resource rows only. Never includes context or <c>time</c>.</returns>
    public IReadOnlyList<PublishedAttribute> List()
    {
        var seen = new HashSet<(AttributeTarget Target, string Type, string Key)>();
        var result = new List<PublishedAttribute>();

        foreach (var map in _options.ResourceMapping.Maps)
        {
            foreach (var member in map.AttributeMembers)
                TryAdd(result, seen, AttributeTarget.Resource, map.Type, member.AttributeName);

            if (map.OwnerMember is not null)
                TryAdd(result, seen, AttributeTarget.Resource, map.Type, ResourceAttributeNames.OwnerId);
        }

        foreach (var map in _options.SubjectMapping.Maps)
        {
            var type = SubjectCatalogType(map);
            foreach (var member in map.AttributeMembers)
                TryAdd(result, seen, AttributeTarget.Subject, type, member.AttributeName);

            if (PublishesTypeKey(map))
                TryAdd(result, seen, AttributeTarget.Subject, type, map.TypeAttributeName);
        }

        foreach (var extra in _options.ExplicitAttributes.Items)
            TryAdd(result, seen, extra.Target, extra.Type, extra.Key);

        return result;
    }

    /// <summary>
    /// Matches subject bundle type: fixed type only when it is stored as the type attribute.
    /// Role-only maps leave that attribute absent, so the catalog type is <c>subject</c>.
    /// </summary>
    private static string SubjectCatalogType(SubjectEntityMap map)
    {
        if (map.FixedTypeValue is not null
            && map.TypeDisposition is SubjectTypeDisposition.Attribute or SubjectTypeDisposition.Both)
            return map.FixedTypeValue;

        return "subject";
    }

    private static bool PublishesTypeKey(SubjectEntityMap map)
    {
        if (map.TypeDisposition is not (SubjectTypeDisposition.Attribute or SubjectTypeDisposition.Both))
            return false;

        return map.FixedTypeValue is not null || map.DiscriminatorMember is not null;
    }

    private static void TryAdd(
        List<PublishedAttribute> result,
        HashSet<(AttributeTarget Target, string Type, string Key)> seen,
        AttributeTarget target,
        string? type,
        string key)
    {
        if (target == AttributeTarget.Context)
            return;
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(key))
            return;
        if (string.Equals(key, "time", StringComparison.Ordinal))
            return;
        if (!seen.Add((target, type, key)))
            return;

        result.Add(new PublishedAttribute(target, type, key));
    }
}
