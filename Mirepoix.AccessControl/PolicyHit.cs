namespace Mirepoix.AccessControl;

public sealed record PolicyHit(
    string PolicyId,
    AuthorizationResult Effect,
    string? Description);
