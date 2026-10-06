using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Authorization.AspNetCore.Tests.TestHost;

/// <summary>
/// In-process test API: AddAccessControl setup, MVC attributes, minimal endpoint extensions, filter PEP.
/// </summary>
public sealed class AccessControlApiFactory : IAsyncLifetime
{
    private WebApplication _app = null!;

    public HttpClient CreateClient() => _app.GetTestClient();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AccessControlApiFactory).Assembly.FullName
        });

        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(DocsController).Assembly);

        var policySet = new PolicySet(
            "v1",
            new[]
            {
                new PolicyModel(
                    "editor-edit",
                    AuthorizationResult.Allow,
                    "EDITOR may edit docs",
                    new IAtom[]
                    {
                        new RoleMembershipAtom(new[] { "EDITOR" }),
                        new OperationMatchAtom(Operation.Parse("doc:edit"))
                    })
            });

        builder.Services.AddSingleton<IResourceHydrator, PassThroughResourceHydrator>();

        builder.Services.AddAccessControl(ac => ac.UseMemoryPolicySet(policySet));

        _app = builder.Build();

        _app.UseAuthentication();
        _app.UseAuthorization();

        _app.MapControllers();

        _app.MapGet("/minimal/docs/{id}", (string id) => Results.Text(id))
            .RequireAccessControl()
            .WithAccessOperation("doc:edit")
            .WithAccessResource("doc");

        _app.MapGet("/filter/docs/{id}", (string id) => Results.Text(id))
            .WithAccessOperation("doc:edit")
            .WithAccessResource("doc")
            .AddEndpointFilter<AccessEndpointFilter>();

        _app.MapGet("/minimal/no-op", () => Results.Text("should-not-run"))
            .RequireAccessControl();

        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

sealed class PassThroughResourceHydrator : IResourceHydrator
{
    public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken)
        => Task.FromResult(partial);
}