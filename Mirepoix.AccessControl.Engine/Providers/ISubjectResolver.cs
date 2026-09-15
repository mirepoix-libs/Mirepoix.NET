namespace Mirepoix.AccessControl.Providers;

public interface ISubjectResolver
{
    Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken);
}
