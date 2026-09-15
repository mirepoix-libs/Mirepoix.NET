namespace Mirepoix.AccessControl;

public sealed record AuthorizationBundle(
    Subject Subject,
    Resource Resource,
    Operation Operation,
    AccessContext Context);
