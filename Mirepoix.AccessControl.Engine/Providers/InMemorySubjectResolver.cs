namespace Mirepoix.AccessControl.Providers;

public sealed class InMemorySubjectResolver : ISubjectResolver
{
    private readonly IReadOnlyDictionary<string, Subject> _subjects;

    public InMemorySubjectResolver(IReadOnlyDictionary<string, Subject> subjects)
    {
        _subjects = subjects;
    }

    public Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken)
    {
        if (!_subjects.TryGetValue(partial.Id, out var subject))
            throw new KeyNotFoundException($"Subject '{partial.Id}' was not found.");

        return Task.FromResult(subject);
    }
}
