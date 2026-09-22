using System.Text.Json;

namespace Mirepoix.AccessControl.Protocol.Http;

/// <summary>
/// Wire representation of an <see cref="AuthorizationRequest"/> for HTTP JSON payloads.
/// </summary>
public sealed class AuthorizationRequestDto
{
    /// <summary>Subject identity, roles, and attributes supplied by the caller.</summary>
    public SubjectDto? Subject { get; set; }

    /// <summary>Resource type, id, and attributes supplied by the caller.</summary>
    public ResourceDto? Resource { get; set; }

    /// <summary>Operation string (maps via <see cref="Operation.Parse"/>).</summary>
    public string? Operation { get; set; }

    /// <summary>Request-scoped time, claims, and values.</summary>
    public AccessContextDto? Context { get; set; }

    /// <summary>Maps a domain authorization request to its wire DTO.</summary>
    public static AuthorizationRequestDto FromDomain(AuthorizationRequest request) =>
        new()
        {
            Subject = SubjectDto.FromDomain(request.Subject),
            Resource = ResourceDto.FromDomain(request.Resource),
            Operation = request.Operation.Value,
            Context = AccessContextDto.FromDomain(request.Context),
        };

    /// <summary>Maps this wire DTO to a domain authorization request.</summary>
    public AuthorizationRequest ToDomain() =>
        new(
            Subject?.ToDomain() ?? new Subject(string.Empty, new HashSet<string>(), new Dictionary<string, object?>()),
            Resource?.ToDomain() ?? new Resource(string.Empty, string.Empty, new Dictionary<string, object?>()),
            Mirepoix.AccessControl.Operation.Parse(Operation ?? throw new ArgumentException("Operation is required.", nameof(Operation))),
            Context?.ToDomain() ?? new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
}

/// <summary>Wire representation of a <see cref="Subject"/>.</summary>
public sealed class SubjectDto
{
    /// <summary>Stable subject identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Role names; omitted or null means an empty set.</summary>
    public IReadOnlyList<string>? Roles { get; set; }

    /// <summary>Attribute bag serialized as JSON elements.</summary>
    public Dictionary<string, JsonElement>? Attributes { get; set; }

    /// <summary>Maps a domain subject to its wire DTO.</summary>
    public static SubjectDto FromDomain(Subject subject) =>
        new()
        {
            Id = subject.Id,
            Roles = subject.Roles.Count == 0 ? null : subject.Roles.ToList(),
            Attributes = AccessControlHttpJson.ToJsonElements(subject.Attributes),
        };

    /// <summary>Maps this wire DTO to a domain subject.</summary>
    public Subject ToDomain() =>
        new(
            Id ?? string.Empty,
            Roles is null ? new HashSet<string>() : new HashSet<string>(Roles),
            AccessControlHttpJson.ToAttributeDictionary(Attributes));
}

/// <summary>Wire representation of a <see cref="Resource"/>.</summary>
public sealed class ResourceDto
{
    /// <summary>Resource type name.</summary>
    public string? Type { get; set; }

    /// <summary>Resource instance id.</summary>
    public string? Id { get; set; }

    /// <summary>Attribute bag serialized as JSON elements.</summary>
    public Dictionary<string, JsonElement>? Attributes { get; set; }

    /// <summary>Maps a domain resource to its wire DTO.</summary>
    public static ResourceDto FromDomain(Resource resource) =>
        new()
        {
            Type = resource.Type,
            Id = resource.Id,
            Attributes = AccessControlHttpJson.ToJsonElements(resource.Attributes),
        };

    /// <summary>Maps this wire DTO to a domain resource.</summary>
    public Resource ToDomain() =>
        new(
            Type ?? string.Empty,
            Id ?? string.Empty,
            AccessControlHttpJson.ToAttributeDictionary(Attributes));
}

/// <summary>Wire representation of an <see cref="AccessContext"/>.</summary>
public sealed class AccessContextDto
{
    /// <summary>Evaluation time; omitted or null when not supplied.</summary>
    public DateTimeOffset? Time { get; set; }

    /// <summary>Identity claims serialized as JSON elements.</summary>
    public Dictionary<string, JsonElement>? Claims { get; set; }

    /// <summary>Application context values serialized as JSON elements.</summary>
    public Dictionary<string, JsonElement>? Values { get; set; }

    /// <summary>Maps a domain access context to its wire DTO.</summary>
    public static AccessContextDto FromDomain(AccessContext context) =>
        new()
        {
            Time = context.Time,
            Claims = AccessControlHttpJson.ToJsonElements(context.Claims),
            Values = AccessControlHttpJson.ToJsonElements(context.Values),
        };

    /// <summary>Maps this wire DTO to a domain access context.</summary>
    public AccessContext ToDomain() =>
        new(
            Time,
            AccessControlHttpJson.ToAttributeDictionary(Claims),
            AccessControlHttpJson.ToAttributeDictionary(Values));
}
