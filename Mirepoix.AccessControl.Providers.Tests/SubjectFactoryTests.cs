namespace Mirepoix.AccessControl.Providers.Tests;

public class SubjectFactoryTests
{
    private sealed class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = "";
        public string Kind { get; set; } = "";
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    }

    [Fact]
    public void Create_maps_attributes_roles_and_type_as_both()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<User>(m => m
            .Id(x => x.Id)
            .Roles(x => x.Roles)
            .Discriminator(x => x.Kind)
            .TypeAsBoth());

        var map = Assert.Single(options.Maps);
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var subject = SubjectFactory.Create(
            new User
            {
                Id = id,
                Email = "a@b.c",
                Kind = "admin",
                Roles = new[] { "editor" },
            },
            map);

        Assert.Equal(id.ToString(), subject.Id);
        Assert.Equal("a@b.c", subject.Attributes["Email"]);
        Assert.Contains("editor", subject.Roles);
        Assert.Contains("admin", subject.Roles);
        Assert.Equal("admin", subject.Attributes["subjectType"]);
        Assert.False(subject.Attributes.ContainsKey("Kind"));
    }

    [Fact]
    public void Create_fixed_type_as_attribute_only()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<User>(m => m.Id(x => x.Id).Type("customer").Exclude(x => x.Roles).Exclude(x => x.Kind));

        var map = Assert.Single(options.Maps);
        var subject = SubjectFactory.Create(
            new User { Id = Guid.NewGuid(), Email = "x@y.z" },
            map);

        Assert.Equal("customer", subject.Attributes["subjectType"]);
        Assert.Empty(subject.Roles);
    }
}
