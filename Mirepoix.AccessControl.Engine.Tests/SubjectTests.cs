using Mirepoix.AccessControl;

public class SubjectTests
{
    [Fact]
    public void Subject_holds_id_roles_and_attributes()
    {
        var subject = new Subject(
            "u1",
            new HashSet<string> { "EDITOR" },
            new Dictionary<string, object?> { ["dept"] = "finance" });

        Assert.Equal("u1", subject.Id);
        Assert.Contains("EDITOR", subject.Roles);
        Assert.Equal("finance", subject.Attributes["dept"]);
    }
}
