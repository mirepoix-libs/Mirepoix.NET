namespace Mirepoix.AccessControl.Protocol.Http;

/// <summary>
/// Wire representation of an <see cref="AccessDecision"/> for HTTP JSON payloads.
/// </summary>
public sealed class AccessDecisionDto
{
    /// <summary>Final allow/deny effect.</summary>
    public AuthorizationResult Result { get; set; }

    /// <summary>Ordered policy hits; never null on the wire (empty array when none).</summary>
    public IReadOnlyList<PolicyHitDto> PolicyHits { get; set; } = Array.Empty<PolicyHitDto>();

    /// <summary>Production status explaining how the decision was produced.</summary>
    public DecisionStatus Status { get; set; }

    /// <summary>Policy-set version for audit/correlation; may be null.</summary>
    public string? PolicySetVersion { get; set; }

    /// <summary>Maps a domain access decision to its wire DTO.</summary>
    public static AccessDecisionDto FromDomain(AccessDecision decision) =>
        new()
        {
            Result = decision.Result,
            PolicyHits = decision.PolicyHits.Select(PolicyHitDto.FromDomain).ToList(),
            Status = decision.Status,
            PolicySetVersion = decision.PolicySetVersion,
        };

    /// <summary>Maps this wire DTO to a domain access decision.</summary>
    public AccessDecision ToDomain() =>
        new(
            Result,
            (PolicyHits ?? Array.Empty<PolicyHitDto>()).Select(hit => hit.ToDomain()).ToList(),
            Status,
            PolicySetVersion);
}

/// <summary>Wire representation of a <see cref="PolicyHit"/>.</summary>
public sealed class PolicyHitDto
{
    /// <summary>Stable policy identifier.</summary>
    public string PolicyId { get; set; } = string.Empty;

    /// <summary>Configured effect for the matching policy.</summary>
    public AuthorizationResult Effect { get; set; }

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Maps a domain policy hit to its wire DTO.</summary>
    public static PolicyHitDto FromDomain(PolicyHit hit) =>
        new()
        {
            PolicyId = hit.PolicyId,
            Effect = hit.Effect,
            Description = hit.Description,
        };

    /// <summary>Maps this wire DTO to a domain policy hit.</summary>
    public PolicyHit ToDomain() =>
        new(PolicyId, Effect, Description);
}
