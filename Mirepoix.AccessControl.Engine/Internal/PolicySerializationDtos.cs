using System.Text.Json;
using System.Text.Json.Serialization;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Internal;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RoleMembershipAtomDto), "role-membership")]
[JsonDerivedType(typeof(AttributeValueAtomDto), "attribute-value")]
[JsonDerivedType(typeof(OperationMatchAtomDto), "operation-match")]
[JsonDerivedType(typeof(SubjectIdEqualsAttributeAtomDto), "subject-id-equals-attribute")]
[JsonDerivedType(typeof(AttributeEqualsAttributeAtomDto), "attribute-equals-attribute")]
internal abstract class AtomDto
{
}

internal sealed class RoleMembershipAtomDto : AtomDto
{
    public List<string> Roles { get; set; } = new();
}

internal sealed class AttributeValueAtomDto : AtomDto
{
    public AttributeTarget Target { get; set; }

    public string Key { get; set; } = "";

    public ComparisonOperator Op { get; set; }

    public JsonElement? Expected { get; set; }
}

internal sealed class OperationMatchAtomDto : AtomDto
{
    public string Pattern { get; set; } = "";
}

internal sealed class SubjectIdEqualsAttributeAtomDto : AtomDto
{
    public AttributeTarget Target { get; set; }

    public string Key { get; set; } = "";
}

internal sealed class AttributeEqualsAttributeAtomDto : AtomDto
{
    public AttributeTarget LeftTarget { get; set; }

    public string LeftKey { get; set; } = "";

    public AttributeTarget RightTarget { get; set; }

    public string RightKey { get; set; } = "";

    public ComparisonOperator Op { get; set; }

    public bool Strict { get; set; } = true;
}

internal sealed class PolicyDto
{
    public string Id { get; set; } = "";

    public AuthorizationResult Effect { get; set; }

    public string? Description { get; set; }

    public List<AtomDto> Atoms { get; set; } = new();
}

internal sealed class PolicySetDto
{
    public string Version { get; set; } = "";

    public List<PolicyDto> Policies { get; set; } = new();
}
