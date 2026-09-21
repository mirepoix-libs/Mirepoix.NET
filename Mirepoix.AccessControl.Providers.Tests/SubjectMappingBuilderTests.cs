namespace Mirepoix.AccessControl.Providers.Tests;

public class SubjectMappingBuilderTests
{
    private sealed class User
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string Department { get; set; } = "";
        public string Role { get; set; } = "";
        public string PasswordHash { get; set; } = "";
    }

    [Fact]
    public void Default_maps_non_id_properties_to_attributes()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<User>(m => m.Id(x => x.Id));

        var map = Assert.Single(options.Maps);
        Assert.Equal("Id", map.IdMember.Name);
        Assert.Contains(map.AttributeMembers, a => a.AttributeName == "Email");
        Assert.Contains(map.AttributeMembers, a => a.AttributeName == "Department");
        Assert.Contains(map.AttributeMembers, a => a.AttributeName == "PasswordHash");
        Assert.DoesNotContain(map.AttributeMembers, a => a.Member.Name == "Id");
    }

    [Fact]
    public void Exclude_and_roles_and_type_configure_map()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<User>(m => m
            .Id(x => x.Id)
            .Exclude(x => x.PasswordHash)
            .Roles(x => x.Role)
            .Type("employee")
            .TypeAsBoth());

        var map = Assert.Single(options.Maps);
        Assert.Equal("employee", map.FixedTypeValue);
        Assert.Equal(SubjectTypeDisposition.Both, map.TypeDisposition);
        Assert.Contains(map.RoleMembers, r => r.Name == "Role");
        Assert.DoesNotContain(map.AttributeMembers, a => a.Member.Name == "PasswordHash");
        Assert.DoesNotContain(map.AttributeMembers, a => a.Member.Name == "Role");
    }

    [Fact]
    public void Duplicate_fixed_type_throws_on_validate()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<User>(m => m.Id(x => x.Id).Type("employee"));
        options.MapEntity<User>(m => m.Id(x => x.Id).Type("employee"));

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("Duplicate subject type value", ex.Message, StringComparison.Ordinal);
    }
}
