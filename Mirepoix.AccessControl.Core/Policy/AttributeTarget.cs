namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Names which part of the bundle an attribute key is resolved against.
/// </summary>
public enum AttributeTarget
{
    /// <summary>Reads from <see cref="Subject.Attributes"/>.</summary>
    Subject,

    /// <summary>Reads from <see cref="Resource.Attributes"/>.</summary>
    Resource,

    /// <summary>
    /// Resolves from context: key <c>time</c> (ordinal ignore case) uses <see cref="AccessContext.Time"/> when set,
    /// then <see cref="AccessContext.Values"/>, then <see cref="AccessContext.Claims"/> (Values win on key clash).
    /// </summary>
    Context
}
