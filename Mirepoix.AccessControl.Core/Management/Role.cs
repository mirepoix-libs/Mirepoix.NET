namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Holds a role catalog entry: stable id plus optional description.
/// Catalog membership is separate from subject role assignment.
/// </summary>
/// <param name="Id">Names the stable role identifier.</param>
/// <param name="Description">Holds an optional human-readable description; may be null.</param>
public sealed record Role(string Id, string? Description);
