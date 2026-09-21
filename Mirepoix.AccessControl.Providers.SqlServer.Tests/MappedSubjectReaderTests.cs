using System.Data;
using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers.SqlServer.Tests;

public class MappedSubjectReaderTests
{
    private sealed class UserRow
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
    }

    [Fact]
    public void ResolveTable_uses_hint_or_type_name()
    {
        var withHint = new SubjectMappingOptions();
        withHint.MapEntity<UserRow>(m => m.Id(x => x.Id).ToTable("users", "app"));
        Assert.Equal("users", MappedSubjectReader.ResolveTable(withHint.Maps[0]));

        var convention = new SubjectMappingOptions();
        convention.MapEntity<UserRow>(m => m.Id(x => x.Id));
        Assert.Equal(nameof(UserRow), MappedSubjectReader.ResolveTable(convention.Maps[0]));
    }

    [Fact]
    public void Materialize_maps_columns_to_properties()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<UserRow>(m => m.Id(x => x.Id).ToTable("users"));
        var map = options.Maps[0];

        var table = new DataTable();
        table.Columns.Add("Id", typeof(string));
        table.Columns.Add("Email", typeof(string));
        table.Rows.Add("u1", "a@b.c");

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var entity = Assert.IsType<UserRow>(MappedSubjectReader.Materialize(reader, map));
        Assert.Equal("u1", entity.Id);
        Assert.Equal("a@b.c", entity.Email);
    }

    [Fact]
    public void Validate_accepts_type_name_convention_for_sql_server()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<UserRow>(m => m.Id(x => x.Id));
        options.Validate(requireStorageTable: true);
    }
}
