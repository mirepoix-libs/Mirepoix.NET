using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;

public class SodAssignmentTests
{
    [Fact]
    public void Assign_conflicting_roles_returns_SodConflict_and_writes_nothing()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        mgr.SodConstraints.Add(new SodConstraint("sod1", new HashSet<string> { "REQUESTOR", "APPROVER" }));
        mgr.RoleCatalog.Add(new Role("REQUESTOR", null));
        mgr.RoleCatalog.Add(new Role("APPROVER", null));
        Assert.Equal(AssignmentOutcome.Assigned, mgr.Assignments.Assign("u1", "REQUESTOR").Outcome);
        var second = mgr.Assignments.Assign("u1", "APPROVER");
        Assert.Equal(AssignmentOutcome.SodConflict, second.Outcome);
        Assert.Equal("sod1", second.ConstraintId);
        Assert.Equal(new HashSet<string> { "REQUESTOR", "APPROVER" }, second.Roles!.ToHashSet());
        Assert.DoesNotContain("APPROVER", mgr.Assignments.GetRoles("u1"));
    }

    [Fact]
    public async Task Assign_is_visible_to_subject_resolver()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        Assert.Equal(AssignmentOutcome.Assigned, mgr.Assignments.Assign("u1", "EDITOR").Outcome);

        var subject = await mgr.SubjectResolver.HydrateAsync(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Contains("EDITOR", subject.Roles);
    }

    [Fact]
    public async Task Revoke_removes_role_from_assignment_store_and_subject_resolver()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        Assert.Equal(AssignmentOutcome.Assigned, mgr.Assignments.Assign("u1", "EDITOR").Outcome);
        Assert.Equal(AssignmentOutcome.Assigned, mgr.Assignments.Assign("u1", "READER").Outcome);

        mgr.Assignments.Revoke("u1", "EDITOR");

        Assert.DoesNotContain("EDITOR", mgr.Assignments.GetRoles("u1"));
        Assert.Contains("READER", mgr.Assignments.GetRoles("u1"));

        var subject = await mgr.SubjectResolver.HydrateAsync(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.DoesNotContain("EDITOR", subject.Roles);
        Assert.Contains("READER", subject.Roles);
    }

    [Fact]
    public void Revoke_unknown_subject_does_not_throw()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        mgr.Assignments.Revoke("nobody", "EDITOR");
        Assert.Empty(mgr.Assignments.GetRoles("nobody"));
    }
}
