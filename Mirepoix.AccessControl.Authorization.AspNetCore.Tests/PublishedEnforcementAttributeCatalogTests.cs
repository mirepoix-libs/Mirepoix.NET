using System.Net;
using Mirepoix.AccessControl.Authorization.AspNetCore;
using Mirepoix.AccessControl.Policy;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Authorization.AspNetCore.Tests;

public class PublishedEnforcementAttributeCatalogTests
{
    [Fact]
    public void List_always_includes_context_time()
    {
        var services = new ServiceCollection();
        services.AddAccessControl(ac => ac.UseMemoryPolicySet(EmptySet()));

        using var provider = services.BuildServiceProvider();
        var listed = provider.GetRequiredService<IPublishedEnforcementAttributeSource>().List();

        var time = Assert.Single(listed);
        Assert.Equal(AttributeTarget.Context, time.Target);
        Assert.Null(time.Type);
        Assert.Equal("time", time.Key);
    }

    [Fact]
    public void AddAccessControlAttribute_publishes_context_keys_once()
    {
        var services = new ServiceCollection();
        services.AddAccessControlAttribute("ip");
        services.AddAccessControlAttribute("tenant");
        services.AddAccessControlAttribute("ip");
        services.AddAccessControlAttribute("time");
        services.AddAccessControlAttribute("IP");
        services.AddAccessControlAttribute(AttributeTarget.Context, "region");
        services.AddAccessControl(ac => ac.UseMemoryPolicySet(EmptySet()));

        using var provider = services.BuildServiceProvider();
        var listed = provider.GetRequiredService<IPublishedEnforcementAttributeSource>().List();

        Assert.Equal(
            new[]
            {
                new PublishedAttribute(AttributeTarget.Context, null, "time"),
                new PublishedAttribute(AttributeTarget.Context, null, "ip"),
                new PublishedAttribute(AttributeTarget.Context, null, "tenant"),
                new PublishedAttribute(AttributeTarget.Context, null, "IP"),
                new PublishedAttribute(AttributeTarget.Context, null, "region")
            },
            listed);
    }

    [Fact]
    public void AddAccessControlAttribute_blank_key_throws_and_is_not_published()
    {
        var services = new ServiceCollection();

        var blank = Assert.Throws<ArgumentException>(() => services.AddAccessControlAttribute(" "));
        var empty = Assert.Throws<ArgumentException>(() => services.AddAccessControlAttribute(string.Empty));
        var missing = Assert.Throws<ArgumentException>(() => services.AddAccessControlAttribute(null!));

        Assert.Equal("key", blank.ParamName);
        Assert.IsType<ArgumentException>(blank);
        Assert.Equal("key", empty.ParamName);
        Assert.Equal("key", missing.ParamName);
        Assert.IsType<ArgumentException>(missing);

        services.AddAccessControl(ac => ac.UseMemoryPolicySet(EmptySet()));
        using var provider = services.BuildServiceProvider();
        var listed = provider.GetRequiredService<IPublishedEnforcementAttributeSource>().List();
        Assert.Equal(new[] { new PublishedAttribute(AttributeTarget.Context, null, "time") }, listed);
    }

    [Theory]
    [InlineData(AttributeTarget.Subject, "dept")]
    [InlineData(AttributeTarget.Resource, "status")]
    public void AddAccessControlAttribute_subject_or_resource_throws_and_is_not_published(
        AttributeTarget target,
        string key)
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<ArgumentException>(() => services.AddAccessControlAttribute(target, key));

        Assert.Equal("target", ex.ParamName);
        Assert.IsType<ArgumentException>(ex);
        Assert.Contains(target.ToString(), ex.Message, StringComparison.OrdinalIgnoreCase);

        services.AddAccessControl(ac => ac.UseMemoryPolicySet(EmptySet()));
        using var provider = services.BuildServiceProvider();
        var listed = provider.GetRequiredService<IPublishedEnforcementAttributeSource>().List();
        Assert.Equal(new[] { new PublishedAttribute(AttributeTarget.Context, null, "time") }, listed);
        Assert.DoesNotContain(listed, row => row.Key == key);
    }

    [Fact]
    public void AddAccessControlAttribute_unknown_target_throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddAccessControlAttribute((AttributeTarget)99, "dept"));

        Assert.Equal("target", ex.ParamName);
    }

    [Fact]
    public void AddAccessControl_registers_live_catalog_sources()
    {
        var services = new ServiceCollection();
        services.AddAccessControlAttribute("ip");
        services.AddAccessControlOperation("invoice:post");
        services.AddAccessControl(ac => ac.UseMemoryPolicySet(EmptySet()));

        using var provider = services.BuildServiceProvider();

        var attributes = provider.GetRequiredService<IPublishedEnforcementAttributeSource>();
        var attributeSource = provider.GetRequiredService<PublishedEnforcementAttributeSource>();
        Assert.Same(attributeSource, attributes);
        Assert.Contains(attributes.List(), row => row.Key == "ip" && row.Target == AttributeTarget.Context);

        var operations = provider.GetRequiredService<IPublishedOperationSource>();
        Assert.Same(provider.GetRequiredService<PublishedOperationSource>(), operations);
        Assert.Contains(operations.List(), row => row.Operation == "invoice:post");
    }

    [Fact]
    public async Task Catalog_get_returns_context_attributes_only()
    {
        await using var host = await CatalogHost.StartAsync(
            services =>
            {
                services.AddAccessControlAttribute("ip");
                services.AddAccessControlAttribute("ip");
            },
            app => app.MapAccessControlAttributes());

        var response = await host.Client.GetAsync(PublishedAttribute.CatalogPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """[{"target":"context","key":"time"},{"target":"context","key":"ip"}]""",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MapAccessControlAttributes_requires_named_policy_only_when_set()
    {
        await using var named = await CatalogHost.StartAsync(map: app => app.MapAccessControlAttributes("Caller"));
        Assert.Equal("Caller", AuthorizePolicy(named.App));

        await using var open = await CatalogHost.StartAsync(map: app => app.MapAccessControlAttributes());
        Assert.Null(AuthorizePolicy(open.App));

        await using var empty = await CatalogHost.StartAsync(map: app => app.MapAccessControlAttributes(string.Empty));
        Assert.Null(AuthorizePolicy(empty.App));
    }

    [Fact]
    public void MapAccessControlAttributes_rejects_null_builder()
    {
        IEndpointRouteBuilder endpoints = null!;

        var ex = Assert.Throws<ArgumentNullException>(() => endpoints.MapAccessControlAttributes());

        Assert.Equal("endpoints", ex.ParamName);
    }

    private static string? AuthorizePolicy(WebApplication app)
    {
        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == PublishedAttribute.CatalogPath);
        return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().SingleOrDefault()?.Policy;
    }

    private static PolicySet EmptySet() => new("v1", Array.Empty<PolicyModel>());

    private sealed class CatalogHost : IAsyncDisposable
    {
        public required WebApplication App { get; init; }

        public HttpClient Client => App.GetTestClient();

        public static async Task<CatalogHost> StartAsync(
            Action<IServiceCollection>? configure = null,
            Action<WebApplication>? map = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(PublishedEnforcementAttributeCatalogTests).Assembly.FullName
            });
            builder.WebHost.UseTestServer();
            configure?.Invoke(builder.Services);
            builder.Services.AddAccessControl(ac => ac.UseMemoryPolicySet(EmptySet()));

            var app = builder.Build();
            map?.Invoke(app);
            await app.StartAsync();
            return new CatalogHost { App = app };
        }

        public async ValueTask DisposeAsync()
        {
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }
}
