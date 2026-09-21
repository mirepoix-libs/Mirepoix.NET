namespace Mirepoix.AccessControl.Providers.Tests;

public class SubjectMappingLookupTests
{
    private sealed class Employee
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
    }

    private sealed class Customer
    {
        public string Id { get; set; } = "";
        public string Tier { get; set; } = "";
    }

    [Fact]
    public async Task Probe_uses_registration_order()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<Employee>(m => m.Id(x => x.Id).Type("employee"));
        options.MapEntity<Customer>(m => m.Id(x => x.Id).Type("customer"));

        var subject = await SubjectMappingLookup.HydrateAsync(
            new Subject("c1", new HashSet<string>(), new Dictionary<string, object?>()),
            options,
            (map, id, _) =>
            {
                if (map.ClrType == typeof(Employee))
                    return Task.FromResult<object?>(null);
                return Task.FromResult<object?>(new Customer { Id = id, Tier = "gold" });
            },
            CancellationToken.None);

        Assert.Equal("c1", subject.Id);
        Assert.Equal("gold", subject.Attributes["Tier"]);
        Assert.Equal("customer", subject.Attributes["subjectType"]);
    }

    [Fact]
    public async Task Hint_selects_map_and_skips_others()
    {
        var probed = new List<Type>();
        var options = new SubjectMappingOptions();
        options.MapEntity<Employee>(m => m.Id(x => x.Id).Type("employee"));
        options.MapEntity<Customer>(m => m.Id(x => x.Id).Type("customer"));

        var partial = new Subject(
            "e1",
            new HashSet<string>(),
            new Dictionary<string, object?> { ["subjectType"] = "employee" });

        var subject = await SubjectMappingLookup.HydrateAsync(
            partial,
            options,
            (map, id, _) =>
            {
                probed.Add(map.ClrType);
                if (map.ClrType == typeof(Employee))
                    return Task.FromResult<object?>(new Employee { Id = id, Title = "dev" });
                return Task.FromResult<object?>(new Customer { Id = id, Tier = "x" });
            },
            CancellationToken.None);

        Assert.Equal(new[] { typeof(Employee) }, probed);
        Assert.Equal("dev", subject.Attributes["Title"]);
    }

    [Fact]
    public async Task Unknown_hint_throws_without_probe_fallback()
    {
        var options = new SubjectMappingOptions();
        options.MapEntity<Employee>(m => m.Id(x => x.Id).Type("employee"));

        var partial = new Subject(
            "e1",
            new HashSet<string>(),
            new Dictionary<string, object?> { ["subjectType"] = "nope" });

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            SubjectMappingLookup.HydrateAsync(
                partial,
                options,
                (_, _, _) => Task.FromResult<object?>(new Employee { Id = "e1" }),
                CancellationToken.None));
    }
}
