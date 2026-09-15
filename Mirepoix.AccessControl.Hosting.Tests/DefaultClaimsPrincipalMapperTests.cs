using System.Security.Claims;
using Mirepoix.AccessControl.Hosting;
using Microsoft.AspNetCore.Http;

public class DefaultClaimsPrincipalMapperTests
{
    [Fact]
    public void CreateSeed_maps_nameidentifier_roles_and_other_claims()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "u1"),
                    new Claim(ClaimTypes.Role, "EDITOR"),
                    new Claim("role", "ADMIN"),
                    new Claim("tenant", "acme")
                },
                authenticationType: "test"))
        };

        var seed = new DefaultClaimsPrincipalMapper().CreateSeed(http);

        Assert.Equal("u1", seed.Subject.Id);
        Assert.Contains("EDITOR", seed.Subject.Roles);
        Assert.Contains("ADMIN", seed.Subject.Roles);
        Assert.Equal("acme", seed.Context.Claims["tenant"]);
        Assert.NotNull(seed.Context.Time);
    }

    [Fact]
    public void CreateSeed_prefers_sub_when_nameidentifier_missing()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("sub", "sub-1") },
                authenticationType: "test"))
        };

        var seed = new DefaultClaimsPrincipalMapper().CreateSeed(http);

        Assert.Equal("sub-1", seed.Subject.Id);
    }
}
