namespace Mirepoix.AccessControl;

/// <summary>
/// Carries request-scoped environmental data for evaluation: optional time, identity claims, and app values.
/// The kernel has no clock and does not parse tokens; callers supply
/// <see cref="Time"/> and <see cref="Claims"/> as data. <see cref="Values"/> holds
/// application-defined keys used by attribute atoms targeting context.
/// </summary>
/// <param name="Time">Supplies evaluation time as data. Null means "no time supplied"; time-based atoms must handle absence.</param>
/// <param name="Claims">Holds identity claims already materialised as a dictionary (not a raw token).</param>
/// <param name="Values">Holds app-supplied context attributes (arbitrary keys for policy atoms).</param>
public sealed record AccessContext(
    DateTimeOffset? Time,
    IReadOnlyDictionary<string, object?> Claims,
    IReadOnlyDictionary<string, object?> Values);
