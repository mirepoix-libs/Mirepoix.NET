namespace Mirepoix.AccessControl.Providers.Entities;

public sealed class PolicySetEntity
{
    public int Id { get; set; }
    public string Version { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
}

public sealed class SubjectEntity
{
    public string SubjectId { get; set; } = "";
    public ICollection<SubjectRoleEntity> Roles { get; set; } = new List<SubjectRoleEntity>();
    public ICollection<SubjectAttributeEntity> Attributes { get; set; } = new List<SubjectAttributeEntity>();
}

public sealed class SubjectRoleEntity
{
    public string SubjectId { get; set; } = "";
    public string Role { get; set; } = "";
    public SubjectEntity? Subject { get; set; }
}

public sealed class SubjectAttributeEntity
{
    public string SubjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ValueJson { get; set; }
    public SubjectEntity? Subject { get; set; }
}

public sealed class ResourceAttributeEntity
{
    public string ResourceType { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? ValueJson { get; set; }
}
