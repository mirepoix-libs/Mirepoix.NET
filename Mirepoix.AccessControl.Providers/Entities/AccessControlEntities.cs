namespace Mirepoix.AccessControl.Providers.Entities;

/// <summary>
/// Holds the row shape for <c>ac_policy_set</c>. Singleton row uses <see cref="Schema.AccessControlSchema.PolicySetSingletonId"/>.
/// <see cref="PayloadJson"/> holds the serialized <c>PolicySet</c> (see <c>PolicySetStorageCodec</c>).
/// </summary>
public sealed class PolicySetEntity
{
    /// <summary>Names the row id (singleton id is 1).</summary>
    public int Id { get; set; }

    /// <summary>Holds the opaque policy set version string.</summary>
    public string Version { get; set; } = "";

    /// <summary>Holds the full policy set JSON payload.</summary>
    public string PayloadJson { get; set; } = "";

    /// <summary>Marks the last update time in UTC.</summary>
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Holds one role-catalog row, with an optional administrator-facing description.
/// </summary>
public sealed class RoleEntity
{
    /// <summary>Names the cataloged role identifier.</summary>
    public string RoleId { get; set; } = "";

    /// <summary>Holds the optional role description.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Holds one separation-of-duties constraint and its mutually exclusive role rows.
/// </summary>
public sealed class SodConstraintEntity
{
    /// <summary>Names the constraint identifier.</summary>
    public string ConstraintId { get; set; } = "";

    /// <summary>Lists the role rows that participate in this constraint.</summary>
    public ICollection<SodConstraintRoleEntity> Roles { get; set; } = new List<SodConstraintRoleEntity>();
}

/// <summary>
/// Holds one role membership in a separation-of-duties constraint.
/// Role identifiers are not required to exist in the role catalog.
/// </summary>
public sealed class SodConstraintRoleEntity
{
    /// <summary>Names the owning separation-of-duties constraint.</summary>
    public string ConstraintId { get; set; } = "";

    /// <summary>Names the mutually exclusive role.</summary>
    public string RoleId { get; set; } = "";

    /// <summary>Holds optional navigation to the owning constraint.</summary>
    public SodConstraintEntity? Constraint { get; set; }
}

/// <summary>
/// Holds the row shape for <c>ac_subject</c> with related attributes.
/// Used by ORM mappings and materialization helpers.
/// </summary>
public sealed class SubjectEntity
{
    /// <summary>Names the subject identifier (primary key).</summary>
    public string SubjectId { get; set; } = "";

    /// <summary>Lists attribute rows for this subject.</summary>
    public ICollection<SubjectAttributeEntity> Attributes { get; set; } = new List<SubjectAttributeEntity>();
}

/// <summary>
/// Holds the row shape for <c>ac_subject_role</c>.
/// </summary>
public sealed class SubjectRoleEntity
{
    /// <summary>Names the owning subject id.</summary>
    public string SubjectId { get; set; } = "";

    /// <summary>Names the role.</summary>
    public string Role { get; set; } = "";
}

/// <summary>
/// Holds the row shape for <c>ac_subject_attribute</c>.
/// </summary>
public sealed class SubjectAttributeEntity
{
    /// <summary>Names the owning subject id.</summary>
    public string SubjectId { get; set; } = "";

    /// <summary>Names the attribute.</summary>
    public string Name { get; set; } = "";

    /// <summary>Holds the attribute value as JSON text; may be null.</summary>
    public string? ValueJson { get; set; }

    /// <summary>Holds optional navigation to the parent subject.</summary>
    public SubjectEntity? Subject { get; set; }
}

/// <summary>
/// Holds the row shape for <c>ac_resource_attribute</c> (no separate resource header table).
/// </summary>
public sealed class ResourceAttributeEntity
{
    /// <summary>Names the resource type segment of the identity.</summary>
    public string ResourceType { get; set; } = "";

    /// <summary>Names the resource id segment of the identity.</summary>
    public string ResourceId { get; set; } = "";

    /// <summary>Names the attribute.</summary>
    public string Name { get; set; } = "";

    /// <summary>Holds the attribute value as JSON text; may be null.</summary>
    public string? ValueJson { get; set; }
}

