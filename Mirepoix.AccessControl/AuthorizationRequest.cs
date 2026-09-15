namespace Mirepoix.AccessControl;

public sealed record AuthorizationRequest(
    Subject Subject,
    Resource Resource,
    Operation Operation,
    AccessContext Context);
