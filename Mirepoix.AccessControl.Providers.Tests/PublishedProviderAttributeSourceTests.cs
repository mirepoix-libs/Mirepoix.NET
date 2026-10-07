using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers.Tests;

public sealed class PublishedProviderAttributeSourceTests
{
    [Fact]
    public void List_resource_map_exports_attribute_include_rename_and_ownerId()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.MapResource<Invoice>(map => map
            .Type("invoice")
            .Id(invoice => invoice.Id)
            .Attribute(invoice => invoice.Status, "status")
            .Include(invoice => invoice.Title, "name")
            .Owner(invoice => invoice.CreatedByUserId)
            .Load((key, _) =>
            {
                var id = key.GetRequired("id");
                return Task.FromResult<Invoice?>(new Invoice { Id = id });
            }));

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(
            list,
            Row(AttributeTarget.Resource, "invoice", "status"),
            Row(AttributeTarget.Resource, "invoice", "name"),
            Row(AttributeTarget.Resource, "invoice", "ownerId"));
    }

    [Fact]
    public void List_resource_default_export_applies_rename_and_ownerId()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.MapResource<Invoice>(map => map
            .Type("invoice")
            .Id(invoice => invoice.Id)
            .Rename(invoice => invoice.Status, "status")
            .Owner(invoice => invoice.CreatedByUserId)
            .Load((key, _) =>
            {
                var id = key.GetRequired("id");
                return Task.FromResult<Invoice?>(new Invoice { Id = id });
            }));

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(
            list,
            Row(AttributeTarget.Resource, "invoice", "status"),
            Row(AttributeTarget.Resource, "invoice", "Title"),
            Row(AttributeTarget.Resource, "invoice", "ownerId"));
        Assert.DoesNotContain(list, row => row.Key == "CreatedByUserId" || row.Key == "Id");
    }

    [Fact]
    public void List_subject_include_all_applies_exclude_rename_and_fixed_type()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.MapSubject<Employee>(map => map
            .Id(employee => employee.Id)
            .IncludeAll()
            .Exclude(employee => employee.PasswordHash)
            .Rename(employee => employee.Department, "dept")
            .Roles(employee => employee.Role)
            .Type("employee"));

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(
            list,
            Row(AttributeTarget.Subject, "employee", "Email"),
            Row(AttributeTarget.Subject, "employee", "dept"),
            Row(AttributeTarget.Subject, "employee", "Kind"),
            Row(AttributeTarget.Subject, "employee", "subjectType"));
        Assert.DoesNotContain(list, row => row.Key is "Id" or "Role" or "PasswordHash");
    }

    [Fact]
    public void List_subject_without_fixed_type_uses_subject_and_skips_type_key()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.MapSubject<Employee>(map => map
            .Id(employee => employee.Id)
            .Include(employee => employee.Email));

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(list, Row(AttributeTarget.Subject, "subject", "Email"));
    }

    [Fact]
    public void List_subject_type_as_role_publishes_subject_and_skips_type_key()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.MapSubject<Employee>(map => map
            .Id(employee => employee.Id)
            .Include(employee => employee.Email)
            .Type("employee")
            .TypeAsRole());

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(list, Row(AttributeTarget.Subject, "subject", "Email"));
    }

    [Fact]
    public void List_subject_type_as_both_publishes_fixed_type_and_type_key()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.MapSubject<Employee>(map => map
            .Id(employee => employee.Id)
            .Include(employee => employee.Email)
            .Type("employee")
            .TypeAsBoth());

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(
            list,
            Row(AttributeTarget.Subject, "employee", "Email"),
            Row(AttributeTarget.Subject, "employee", "subjectType"));
    }

    [Fact]
    public void List_subject_discriminator_publishes_type_key_not_discriminator()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.MapSubject<Employee>(map => map
            .Id(employee => employee.Id)
            .Include(employee => employee.Email)
            .Discriminator(employee => employee.Kind)
            .TypeAttributeName("kind")
            .TypeAsAttribute());

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(
            list,
            Row(AttributeTarget.Subject, "subject", "Email"),
            Row(AttributeTarget.Subject, "subject", "kind"));
        Assert.DoesNotContain(list, row => row.Key == "Kind");
    }

    [Fact]
    public void AddAttribute_context_throws()
    {
        var options = new AgnosticAccessControlProviderOptions();

        var ex = Assert.Throws<ArgumentException>(() =>
            options.AddAttribute(AttributeTarget.Context, "tenant"));

        Assert.Contains("context", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddAttribute_blank_key_throws()
    {
        var options = new AgnosticAccessControlProviderOptions();

        var ex = Assert.Throws<ArgumentException>(() =>
            options.AddAttribute(AttributeTarget.Subject, " "));

        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void AddAttribute_subject_omitted_type_defaults_to_subject()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.AddAttribute(AttributeTarget.Subject, "dept");

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(list, Row(AttributeTarget.Subject, "subject", "dept"));
    }

    [Fact]
    public void AddAttribute_resource_without_type_throws()
    {
        var options = new AgnosticAccessControlProviderOptions();

        var ex = Assert.Throws<ArgumentException>(() =>
            options.AddAttribute(AttributeTarget.Resource, "status"));

        Assert.Equal("type", ex.ParamName);
    }

    [Fact]
    public void AddAttribute_duplicate_is_ignored()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.AddAttribute(AttributeTarget.Subject, "dept");
        options.AddAttribute(AttributeTarget.Subject, "dept", "subject");
        options.AddAttribute(AttributeTarget.Resource, "status", "doc");
        options.MapResource<Invoice>(map => map
            .Type("doc")
            .Id(invoice => invoice.Id)
            .Attribute(invoice => invoice.Status, "status")
            .Load((key, _) =>
            {
                var id = key.GetRequired("id");
                return Task.FromResult<Invoice?>(new Invoice { Id = id });
            }));

        var list = new PublishedProviderAttributeSource(options).List();

        AssertCatalog(
            list,
            Row(AttributeTarget.Subject, "subject", "dept"),
            Row(AttributeTarget.Resource, "doc", "status"));
    }

    [Fact]
    public void List_never_emits_context_or_time()
    {
        var options = new AgnosticAccessControlProviderOptions();
        options.AddAttribute(AttributeTarget.Subject, "time");
        options.MapResource<Timed>(map => map
            .Type("doc")
            .Id(item => item.Id)
            .Attribute(item => item.Time, "time")
            .Load((key, _) =>
            {
                var id = key.GetRequired("id");
                return Task.FromResult<Timed?>(new Timed { Id = id });
            }));

        var list = new PublishedProviderAttributeSource(options).List();

        Assert.Empty(list);
    }

    [Fact]
    public void AddAccessControlProviders_registers_singleton_source_for_empty_maps()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(_ => { });

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IPublishedProviderAttributeSource));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IPublishedProviderAttributeSource>();
        var second = provider.GetRequiredService<IPublishedProviderAttributeSource>();

        Assert.Same(first, second);
        Assert.Empty(first.List());
    }

    [Fact]
    public void AddAccessControlProviders_source_lists_configured_maps_and_extras()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(options => options
            .MapResource<Invoice>(map => map
                .Type("invoice")
                .Id(invoice => invoice.Id)
                .Owner(invoice => invoice.CreatedByUserId)
                .Attribute(invoice => invoice.Status, "status")
                .Load((key, _) =>
            {
                var id = key.GetRequired("id");
                return Task.FromResult<Invoice?>(new Invoice { Id = id });
            }))
            .AddAttribute(AttributeTarget.Subject, "dept"));

        using var provider = services.BuildServiceProvider();
        var list = provider.GetRequiredService<IPublishedProviderAttributeSource>().List();

        AssertCatalog(
            list,
            Row(AttributeTarget.Resource, "invoice", "status"),
            Row(AttributeTarget.Resource, "invoice", "ownerId"),
            Row(AttributeTarget.Subject, "subject", "dept"));
    }

    private static PublishedAttribute Row(AttributeTarget target, string type, string key) =>
        new(target, type, key);

    private static void AssertCatalog(IReadOnlyList<PublishedAttribute> actual, params PublishedAttribute[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        foreach (var row in expected)
            Assert.Contains(row, actual);
    }

    private sealed class Invoice
    {
        public string Id { get; set; } = "";

        public string Status { get; set; } = "";

        public string Title { get; set; } = "";

        public string CreatedByUserId { get; set; } = "";
    }

    private sealed class Employee
    {
        public string Id { get; set; } = "";

        public string Email { get; set; } = "";

        public string Department { get; set; } = "";

        public string Role { get; set; } = "";

        public string PasswordHash { get; set; } = "";

        public string Kind { get; set; } = "";
    }

    private sealed class Timed
    {
        public string Id { get; set; } = "";

        public string Time { get; set; } = "";
    }
}
