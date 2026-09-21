using System.Reflection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Maps a CLR data-model type to a hydrated <see cref="Subject"/>.
/// Built by <see cref="SubjectEntityMapBuilder{T}"/>; consumed by <see cref="SubjectFactory"/> and
/// <see cref="SubjectMappingLookup"/>.
/// </summary>
public sealed class SubjectEntityMap
{
    /// <summary>Names the CLR type this map describes.</summary>
    public required Type ClrType { get; init; }

    /// <summary>Names the property that supplies <see cref="Subject.Id"/> (required; never null after build).</summary>
    public required PropertyInfo IdMember { get; init; }

    /// <summary>Lists properties copied into <see cref="Subject.Attributes"/> (name may be renamed).</summary>
    public required IReadOnlyList<MappedAttributeMember> AttributeMembers { get; init; }

    /// <summary>Lists properties folded into <see cref="Subject.Roles"/> (string, enumerable, or coerced scalar).</summary>
    public required IReadOnlyList<PropertyInfo> RoleMembers { get; init; }

    /// <summary>
    /// Holds the fixed type token for multi-table maps (e.g. <c>"employee"</c>). Used for hint matching and
    /// applied via <see cref="TypeDisposition"/>. Mutually exclusive in practice with per-row
    /// discriminators for hint selection (see <see cref="MatchesTypeHint"/>).
    /// </summary>
    public string? FixedTypeValue { get; init; }

    /// <summary>
    /// Holds the optional per-row type discriminator property. Excluded from attribute export; value is applied
    /// via <see cref="TypeDisposition"/> when no <see cref="FixedTypeValue"/> is set.
    /// </summary>
    public PropertyInfo? DiscriminatorMember { get; init; }

    /// <summary>Controls how the type value is written onto the subject (attribute, role, or both).</summary>
    public SubjectTypeDisposition TypeDisposition { get; init; } = SubjectTypeDisposition.Attribute;

    /// <summary>
    /// Names the attribute key used when disposition includes an attribute. Defaults to
    /// <see cref="SubjectMappingOptions.DefaultHintAttributeName"/>.
    /// </summary>
    public string TypeAttributeName { get; init; } = SubjectMappingOptions.DefaultHintAttributeName;

    /// <summary>Holds optional ADO table/column hints; may be null.</summary>
    public SubjectStorageHints? StorageHints { get; init; }

    /// <summary>
    /// Evaluates whether this map is a candidate for a type hint during lookup.
    /// Fixed-type maps: ordinal equality with <see cref="FixedTypeValue"/>.
    /// Discriminator-only maps: return true here so the row can be fetched; equality is checked
    /// after fetch. Maps with neither never match a hint.
    /// </summary>
    /// <param name="hint">Non-empty hint string from the partial subject.</param>
    /// <returns><see langword="true"/> if this map should be probed for that hint.</returns>
    public bool MatchesTypeHint(string hint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);

        if (FixedTypeValue is not null)
            return string.Equals(FixedTypeValue, hint, StringComparison.Ordinal);

        // Discriminator-only maps match any hint at selection time only when the
        // fetched row's discriminator equals the hint (checked after fetch).
        return DiscriminatorMember is not null;
    }
}

/// <summary>
/// Holds one CLR property exported as a subject attribute under <see cref="AttributeName"/>.
/// </summary>
public sealed class MappedAttributeMember
{
    /// <summary>Names the source property on the entity.</summary>
    public required PropertyInfo Member { get; init; }

    /// <summary>Names the attribute key written to <see cref="Subject.Attributes"/>.</summary>
    public required string AttributeName { get; init; }
}
