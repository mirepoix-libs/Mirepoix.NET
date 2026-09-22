using Mirepoix.AccessControl.Engine.Tests.Fakes;
using Mirepoix.AccessControl.Management;

public class SubjectStoreContractTests
{
    [Fact]
    public void AssignRole_uses_subject_store_contract()
    {
        ISubjectStore store = new RecordingSubjectStore();

        var result = store.AssignRole("subject-1", "role-1");

        var recordingStore = Assert.IsType<RecordingSubjectStore>(store);
        Assert.Equal(AssignmentOutcome.Assigned, result.Outcome);
        Assert.Equal("subject-1", recordingStore.AssignedSubjectId);
        Assert.Equal("role-1", recordingStore.AssignedRoleId);
    }
}
