namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Looks up subjects in memory by <see cref="Subject.Id"/>. Missing id throws
/// <see cref="KeyNotFoundException"/> (maps to hydration failure in <see cref="LocalAccessChecker"/>).
/// Returns the stored subject as-is; does not merge with the partial's roles/attributes.
/// </summary>
public sealed class InMemorySubjectResolver : ISubjectResolver
{
    private readonly IReadOnlyDictionary<string, Subject> _subjects;

    /// <summary>
    /// Creates a resolver over a static id-to-subject map.
    /// </summary>
    /// <param name="subjects">Lookup table keyed by subject id.</param>
    public InMemorySubjectResolver(IReadOnlyDictionary<string, Subject> subjects)
    {
        _subjects = subjects;
    }

    /// <summary>
    /// Looks up <paramref name="partial"/>.<see cref="Subject.Id"/>.
    /// </summary>
    /// <param name="partial">Partial subject (id used as key).</param>
    /// <param name="cancellationToken">Unused; completed synchronously.</param>
    /// <returns>Stored subject.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the id is not in the map.</exception>
    public Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken)
    {
        if (!_subjects.TryGetValue(partial.Id, out var subject))
            throw new KeyNotFoundException($"Subject '{partial.Id}' was not found.");

        return Task.FromResult(subject);
    }
}
