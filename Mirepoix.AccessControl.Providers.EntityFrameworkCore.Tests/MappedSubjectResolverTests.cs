using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers.EntityFrameworkCore.Tests;

public class MappedSubjectResolverTests
{
    private sealed class AppUser
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string Department { get; set; } = "";
    }

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

    public abstract class Person
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
    }

    public sealed class Staff : Person { }

    public sealed class Guest : Person { }

    private sealed class UsersDbContext : DbContext
    {
        public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Person> People => Set<Person>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>()
                .HasDiscriminator(x => x.Kind)
                .HasValue<Staff>("staff")
                .HasValue<Guest>("guest");
            modelBuilder.ApplyAccessControl(SubjectStorageLayout.MappedLibraryRoles);
        }
    }

    private sealed class LibraryRolesDbContext : DbContext
    {
        public LibraryRolesDbContext(DbContextOptions<LibraryRolesDbContext> options) : base(options) { }

        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<SubjectRoleEntity> SubjectRoles => Set<SubjectRoleEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>();
            modelBuilder.ApplyAccessControl(SubjectStorageLayout.MappedLibraryRoles);
        }
    }

    [Fact]
    public async Task Single_entity_map_hydrates_attributes()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<UsersDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddAccessControlSubjectProviders<UsersDbContext>(o =>
            o.MapSubject<AppUser>(m => m.Id(x => x.Id)));

        await using var sp = services.BuildServiceProvider();
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            db.Users.Add(new AppUser { Id = "u1", Email = "a@b.c", Department = "eng" });
            await db.SaveChangesAsync();
        }

        var resolver = sp.GetRequiredService<ISubjectResolver>();
        var subject = await resolver.HydrateAsync(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("u1", subject.Id);
        Assert.Equal("a@b.c", subject.Attributes["Email"]);
        Assert.Equal("eng", subject.Attributes["Department"]);
    }

    [Fact]
    public async Task Multi_table_probe_and_hint()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<UsersDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddAccessControlSubjectProviders<UsersDbContext>(o =>
        {
            o.MapSubject<Employee>(m => m.Id(x => x.Id).Type("employee").TypeAsRole());
            o.MapSubject<Customer>(m => m.Id(x => x.Id).Type("customer"));
        });

        await using var sp = services.BuildServiceProvider();
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            db.Employees.Add(new Employee { Id = "e1", Title = "dev" });
            db.Customers.Add(new Customer { Id = "c1", Tier = "gold" });
            await db.SaveChangesAsync();
        }

        var resolver = sp.GetRequiredService<ISubjectResolver>();

        var probed = await resolver.HydrateAsync(
            new Subject("c1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);
        Assert.Equal("gold", probed.Attributes["Tier"]);
        Assert.Equal("customer", probed.Attributes["subjectType"]);

        var hinted = await resolver.HydrateAsync(
            new Subject(
                "e1",
                new HashSet<string>(),
                new Dictionary<string, object?> { ["subjectType"] = "employee" }),
            CancellationToken.None);
        Assert.Equal("dev", hinted.Attributes["Title"]);
        Assert.Contains("employee", hinted.Roles);
        Assert.False(hinted.Attributes.ContainsKey("subjectType"));
    }

    [Fact]
    public async Task Tph_discriminator_maps_type_attribute()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<UsersDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddAccessControlSubjectProviders<UsersDbContext>(o =>
            o.MapSubject<Person>(m => m.Id(x => x.Id).Discriminator(x => x.Kind).TypeAsBoth()));

        await using var sp = services.BuildServiceProvider();
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            db.People.Add(new Staff { Id = "s1", Name = "Sam", Kind = "staff" });
            await db.SaveChangesAsync();
        }

        var resolver = sp.GetRequiredService<ISubjectResolver>();
        var subject = await resolver.HydrateAsync(
            new Subject("s1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("Sam", subject.Attributes["Name"]);
        Assert.Equal("staff", subject.Attributes["subjectType"]);
        Assert.Contains("staff", subject.Roles);
    }

    [Fact]
    public async Task Mapped_subject_without_role_members_unions_library_roles()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<LibraryRolesDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddAccessControlSubjectProviders<LibraryRolesDbContext>(o =>
            o.MapSubject<AppUser>(m => m.Id(x => x.Id).Type("employee").TypeAsRole()));

        await using var sp = services.BuildServiceProvider();
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryRolesDbContext>();
            db.Users.Add(new AppUser { Id = "u1", Email = "a@b.c", Department = "eng" });
            db.SubjectRoles.Add(new SubjectRoleEntity { SubjectId = "u1", Role = "EDITOR" });
            await db.SaveChangesAsync();
        }

        var resolver = sp.GetRequiredService<ISubjectResolver>();
        var subject = await resolver.HydrateAsync(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal(2, subject.Roles.Count);
        Assert.Contains("employee", subject.Roles);
        Assert.Contains("EDITOR", subject.Roles);
    }
}
