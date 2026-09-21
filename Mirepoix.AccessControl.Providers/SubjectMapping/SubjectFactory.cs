using System.Collections;
using System.Globalization;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Builds a <see cref="Subject"/> from a CLR instance and its <see cref="SubjectEntityMap"/>.
/// Coerces id/discriminator/role scalars with invariant culture. Role members accept a string,
/// <see cref="IEnumerable{T}"/> of strings, other enumerables (items coerced), or a single scalar.
/// Whitespace-only role strings are skipped. Null id throws.
/// </summary>
public static class SubjectFactory
{
    /// <summary>
    /// Maps <paramref name="entity"/> to a <see cref="Subject"/> using <paramref name="map"/>.
    /// </summary>
    /// <param name="entity">Instance whose runtime type must be assignable to <see cref="SubjectEntityMap.ClrType"/>.</param>
    /// <param name="map">Declarative map for that type.</param>
    /// <returns>Hydrated subject (id, roles, attributes).</returns>
    /// <exception cref="ArgumentException">Thrown when the entity type does not match the map.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the id property value is null.</exception>
    public static Subject Create(object entity, SubjectEntityMap map)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(map);

        if (!map.ClrType.IsInstanceOfType(entity))
        {
            throw new ArgumentException(
                $"Entity type '{entity.GetType().Name}' is not assignable to mapped type '{map.ClrType.Name}'.",
                nameof(entity));
        }

        var idValue = map.IdMember.GetValue(entity);
        var id = CoerceId(idValue)
            ?? throw new InvalidOperationException(
                $"Subject id on '{map.ClrType.Name}.{map.IdMember.Name}' was null.");

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var roleMember in map.RoleMembers)
            AddRoles(roles, roleMember.GetValue(entity));

        var attributes = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var attr in map.AttributeMembers)
            attributes[attr.AttributeName] = attr.Member.GetValue(entity);

        var typeValue = map.FixedTypeValue;
        if (typeValue is null && map.DiscriminatorMember is not null)
            typeValue = CoerceId(map.DiscriminatorMember.GetValue(entity));

        if (typeValue is not null)
            ApplyType(map, typeValue, roles, attributes);

        return new Subject(id, roles, attributes);
    }

    /// <summary>
    /// Coerces a CLR value to a string id/token: null stays null; strings pass through;
    /// <see cref="IFormattable"/> / other values use invariant <see cref="Convert.ToString(object?, IFormatProvider)"/>.
    /// </summary>
    /// <param name="value">Raw property value.</param>
    /// <returns>String form, or null.</returns>
    public static string? CoerceId(object? value) =>
        value switch
        {
            null => null,
            string s => s,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };

    private static void ApplyType(
        SubjectEntityMap map,
        string typeValue,
        HashSet<string> roles,
        Dictionary<string, object?> attributes)
    {
        switch (map.TypeDisposition)
        {
            case SubjectTypeDisposition.Role:
                roles.Add(typeValue);
                break;
            case SubjectTypeDisposition.Both:
                roles.Add(typeValue);
                attributes[map.TypeAttributeName] = typeValue;
                break;
            default:
                attributes[map.TypeAttributeName] = typeValue;
                break;
        }
    }

    private static void AddRoles(HashSet<string> roles, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case string s when !string.IsNullOrWhiteSpace(s):
                roles.Add(s);
                return;
            case IEnumerable<string> strings:
                foreach (var s in strings)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        roles.Add(s);
                }
                return;
            case IEnumerable enumerable:
                foreach (var item in enumerable)
                {
                    var coerced = CoerceId(item);
                    if (!string.IsNullOrWhiteSpace(coerced))
                        roles.Add(coerced);
                }
                return;
            default:
                var single = CoerceId(value);
                if (!string.IsNullOrWhiteSpace(single))
                    roles.Add(single);
                return;
        }
    }
}
