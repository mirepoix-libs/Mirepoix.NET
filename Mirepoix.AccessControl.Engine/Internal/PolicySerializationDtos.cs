using System.Text.Json;
using System.Text.Json.Serialization;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Internal;

/// <summary>
/// Holds the serde DTO base for atoms. Discriminator property <c>type</c> must match <see cref="IAtom.Name"/>
/// strings on the public atom types. Keep derived discriminators in sync when adding atoms.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RoleMembershipAtomDto), "role-membership")]
[JsonDerivedType(typeof(AttributeValueAtomDto), "attribute-value")]
[JsonDerivedType(typeof(OperationMatchAtomDto), "operation-match")]
[JsonDerivedType(typeof(SubjectIdEqualsAttributeAtomDto), "subject-id-equals-attribute")]
[JsonDerivedType(typeof(AttributeEqualsAttributeAtomDto), "attribute-equals-attribute")]
internal abstract class AtomDto
{
}

/// <summary>Holds the DTO for <see cref="RoleMembershipAtom"/>.</summary>
internal sealed class RoleMembershipAtomDto : AtomDto
{
    /// <summary>Lists role names accepted by the atom.</summary>
    public List<string> Roles { get; set; } = new();
}

/// <summary>Holds the DTO for <see cref="AttributeValueAtom"/>. Expected is kept as <see cref="JsonElement"/> until materialization.</summary>
internal sealed class AttributeValueAtomDto : AtomDto
{
    /// <summary>Names the attribute target.</summary>
    public AttributeTarget Target { get; set; }

    /// <summary>Names the attribute key.</summary>
    public string Key { get; set; } = "";

    /// <summary>Holds the comparison operator.</summary>
    public ComparisonOperator Op { get; set; }

    /// <summary>Holds the expected value as raw JSON (null when omitted).</summary>
    public JsonElement? Expected { get; set; }
}

/// <summary>Holds the DTO for <see cref="OperationMatchAtom"/>; pattern is the operation string, not a parsed <see cref="Operation"/>.</summary>
internal sealed class OperationMatchAtomDto : AtomDto
{
    /// <summary>Holds the operation pattern string passed to <see cref="Operation.Parse"/>.</summary>
    public string Pattern { get; set; } = "";
}

/// <summary>Holds the DTO for <see cref="SubjectIdEqualsAttributeAtom"/>.</summary>
internal sealed class SubjectIdEqualsAttributeAtomDto : AtomDto
{
    /// <summary>Names the attribute target.</summary>
    public AttributeTarget Target { get; set; }

    /// <summary>Names the attribute key compared to subject id.</summary>
    public string Key { get; set; } = "";
}

/// <summary>Holds the DTO for <see cref="AttributeEqualsAttributeAtom"/>.</summary>
internal sealed class AttributeEqualsAttributeAtomDto : AtomDto
{
    /// <summary>Names the left attribute target.</summary>
    public AttributeTarget LeftTarget { get; set; }

    /// <summary>Names the left attribute key.</summary>
    public string LeftKey { get; set; } = "";

    /// <summary>Names the right attribute target.</summary>
    public AttributeTarget RightTarget { get; set; }

    /// <summary>Names the right attribute key.</summary>
    public string RightKey { get; set; } = "";

    /// <summary>Holds the comparison operator.</summary>
    public ComparisonOperator Op { get; set; }

    /// <summary>Marks missing-attribute strictness; defaults to true to match the public ctor.</summary>
    public bool Strict { get; set; } = true;
}

/// <summary>Holds the DTO for <see cref="Policy"/>.</summary>
internal sealed class PolicyDto
{
    /// <summary>Names the policy id.</summary>
    public string Id { get; set; } = "";

    /// <summary>Holds the policy effect.</summary>
    public AuthorizationResult Effect { get; set; }

    /// <summary>Holds an optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Lists atom payloads (polymorphic).</summary>
    public List<AtomDto> Atoms { get; set; } = new();
}

/// <summary>Holds the DTO for <see cref="PolicySet"/>.</summary>
internal sealed class PolicySetDto
{
    /// <summary>Names the opaque version string.</summary>
    public string Version { get; set; } = "";

    /// <summary>Lists policies in evaluation order.</summary>
    public List<PolicyDto> Policies { get; set; } = new();
}
