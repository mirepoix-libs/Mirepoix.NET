using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Management.EntityFrameworkCore.Tests;

public sealed class MappedSubjectRoleMergeIntegrationTests
{
    [Fact]
    public async Task Native_assignment_hydrates_role_and_sod_blocks_conflicting_role()
    {
        await using var database = await TestDatabase.TryCreateAsync();
        if (database is null)
            return;

        var services = new ServiceCollection();
        services.AddAccessControlProviders(options =>
            options.ConfigureDb = db => db.UseSqlServer(database.ConnectionString));
        services.AddAccessControlManagement();

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<AccessControlSchemaApplier>().ApplyAsync();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessControlDbContext>();
            db.Subjects.Add(new SubjectEntity { SubjectId = "alice" });
            await db.SaveChangesAsync();

            await scope.ServiceProvider.GetRequiredService<ISodConstraintStore>().AddAsync(
                new SodConstraint(
                    "finance-sod",
                    new HashSet<string> { "REQUESTER", "APPROVER" }));

            var subjects = scope.ServiceProvider.GetRequiredService<ISubjectStore>();
            Assert.Equal(
                AssignmentOutcome.Assigned,
                (await subjects.AssignRoleAsync("alice", "REQUESTER")).Outcome);
        }

        var hydrated = await provider.GetRequiredService<ISubjectResolver>().HydrateAsync(
            new Subject("alice", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);
        Assert.Equal(new HashSet<string> { "REQUESTER" }, hydrated.Roles);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationSubjects =
            verificationScope.ServiceProvider.GetRequiredService<ISubjectStore>();
        var conflict = await verificationSubjects.AssignRoleAsync("alice", "APPROVER");

        Assert.Equal(AssignmentOutcome.SodConflict, conflict.Outcome);
        Assert.Equal("finance-sod", conflict.ConstraintId);
        Assert.Equal(
            new HashSet<string> { "REQUESTER" },
            await verificationSubjects.GetRolesAsync("alice"));
    }

    [Fact]
    public async Task Mapped_library_roles_assignment_is_unioned_during_hydration()
    {
        await using var database = await TestDatabase.TryCreateAsync();
        if (database is null)
            return;

        var services = new ServiceCollection();
        services.AddDbContext<LibraryRolesDbContext>(
            db => db.UseSqlServer(database.ConnectionString));
        services.AddAccessControlProviders<LibraryRolesDbContext>(options =>
            options.MapSubject<AppUser>(map => map
                .Id(user => user.Id)
                .Type("employee")
                .TypeAsRole()));
        services.AddAccessControlManagement<LibraryRolesDbContext>();

        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryRolesDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new AppUser { Id = "mapped-alice", Role = "IGNORED" });
            await db.SaveChangesAsync();

            var subjects = scope.ServiceProvider.GetRequiredService<ISubjectStore>();
            Assert.Equal(
                AssignmentOutcome.Assigned,
                (await subjects.AssignRoleAsync("mapped-alice", "EDITOR")).Outcome);
        }

        var hydrated = await provider.GetRequiredService<ISubjectResolver>().HydrateAsync(
            new Subject("mapped-alice", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal(new HashSet<string> { "employee", "EDITOR" }, hydrated.Roles);
    }

    [Fact]
    public async Task Mapped_app_owned_roles_omit_subject_store_and_subject_role_model()
    {
        await using var database = await TestDatabase.TryCreateAsync();
        if (database is null)
            return;

        var services = new ServiceCollection();
        services.AddDbContext<AppOwnedRolesDbContext>(
            db => db.UseSqlServer(database.ConnectionString));
        services.AddAccessControlProviders<AppOwnedRolesDbContext>(options =>
            options.MapSubject<AppUser>(map => map
                .Id(user => user.Id)
                .Roles(user => user.Role)));
        services.AddAccessControlManagement<AppOwnedRolesDbContext>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppOwnedRolesDbContext>();
        await db.Database.EnsureCreatedAsync();

        Assert.Null(scope.ServiceProvider.GetService<ISubjectStore>());
        Assert.Null(db.Model.FindEntityType(typeof(SubjectRoleEntity)));

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = @name";
        command.Parameters.Add(new SqlParameter("@name", AccessControlSchema.SubjectRoleTable));
        await db.Database.OpenConnectionAsync();
        Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private sealed class LibraryRolesDbContext(
        DbContextOptions<LibraryRolesDbContext> options) : DbContext(options)
    {
        public DbSet<AppUser> Users => Set<AppUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>().HasKey(user => user.Id);
            modelBuilder.ApplyAccessControl(SubjectStorageLayout.MappedLibraryRoles);
        }
    }

    private sealed class AppOwnedRolesDbContext(
        DbContextOptions<AppOwnedRolesDbContext> options) : DbContext(options)
    {
        public DbSet<AppUser> Users => Set<AppUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppUser>().HasKey(user => user.Id);
            modelBuilder.ApplyAccessControl(SubjectStorageLayout.MappedAppOwnedRoles);
        }
    }

    private sealed class AppUser
    {
        public string Id { get; set; } = "";
        public string Role { get; set; } = "";
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private static readonly string? ServerConnectionString = ResolveServerConnectionString();
        private readonly string _databaseName;

        private TestDatabase(string databaseName, string connectionString)
        {
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<TestDatabase?> TryCreateAsync()
        {
            if (ServerConnectionString is null)
                return null;

            var databaseName = $"MirepoixManagementEf_{Guid.NewGuid():N}";
            try
            {
                await using var connection = new SqlConnection(ServerConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}]";
                await command.ExecuteNonQueryAsync();

                var builder = new SqlConnectionStringBuilder(ServerConnectionString)
                {
                    InitialCatalog = databaseName,
                };
                return new TestDatabase(databaseName, builder.ConnectionString);
            }
            catch
            {
                return null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            if (ServerConnectionString is null)
                return;

            await using var connection = new SqlConnection(ServerConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                 ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                 DROP DATABASE [{_databaseName}];
                 """;
            await command.ExecuteNonQueryAsync();
        }

        private static string? ResolveServerConnectionString()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("MIREPOIX_SQLSERVER");
            var candidate = string.IsNullOrWhiteSpace(fromEnvironment)
                ? "Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
                : fromEnvironment;

            try
            {
                using var connection = new SqlConnection(candidate);
                connection.Open();
                return candidate;
            }
            catch
            {
                return null;
            }
        }
    }
}
