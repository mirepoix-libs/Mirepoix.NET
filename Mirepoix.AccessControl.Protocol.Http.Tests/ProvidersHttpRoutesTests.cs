using Mirepoix.AccessControl.Protocol.Http;

namespace Mirepoix.AccessControl.Protocol.Http.Tests;

public sealed class ProvidersHttpRoutesTests
{
    [Fact]
    public void Absolute_PIP_hydrate_paths_are_stable()
    {
        Assert.Equal(
            "/access-control/providers/subject/hydrate",
            AccessControlHttpRoutes.AbsoluteProvidersSubjectHydratePath);
        Assert.Equal(
            "/access-control/providers/resource/hydrate",
            AccessControlHttpRoutes.AbsoluteProvidersResourceHydratePath);
        Assert.Equal(
            "/access-control/providers/context/hydrate",
            AccessControlHttpRoutes.AbsoluteProvidersContextHydratePath);
    }
}
