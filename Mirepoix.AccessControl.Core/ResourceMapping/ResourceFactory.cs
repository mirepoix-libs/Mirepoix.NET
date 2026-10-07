using System.Globalization;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Creates access-control resources from mapped domain entities.
/// </summary>
public static class ResourceFactory
{
    /// <summary>Creates a resource from an entity and its map.</summary>
    public static Resource Create(object entity, ResourceEntityMap map)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(map);

        if (!map.ClrType.IsInstanceOfType(entity))
        {
            throw new ArgumentException(
                $"Entity type '{entity.GetType().Name}' is not assignable to mapped type '{map.ClrType.Name}'.",
                nameof(entity));
        }

        var parts = new (string name, string value)[map.KeyMembers.Count];
        for (var i = 0; i < map.KeyMembers.Count; i++)
        {
            var keyMember = map.KeyMembers[i];
            var value = CoerceId(keyMember.Member.GetValue(entity))
                ?? throw new InvalidOperationException(
                    $"Resource key part '{keyMember.PartName}' on '{map.ClrType.Name}.{keyMember.Member.Name}' was null.");
            parts[i] = (keyMember.PartName, value);
        }

        var key = ResourceKey.From(parts);

        var attributes = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var attribute in map.AttributeMembers)
            attributes[attribute.AttributeName] = attribute.Member.GetValue(entity);

        if (map.OwnerMember is not null)
            attributes[ResourceAttributeNames.OwnerId] = map.OwnerMember.GetValue(entity);

        return new Resource(map.Type, key, attributes);
    }

    private static string? CoerceId(object? value) =>
        value switch
        {
            null => null,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
}
