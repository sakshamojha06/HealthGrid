using HealthGrid.Application.Abstractions;

namespace HealthGrid.Api.Tests;

public sealed class ScopeTests
{
    [Fact]
    public void PhcUserCannotAccessAnotherPhcOrDistrict()
    {
        var district = Guid.NewGuid();
        var phc = Guid.NewGuid();
        var scope = new UserScope(district, phc, false);

        Assert.True(scope.CanAccess(district, phc));
        Assert.False(scope.CanAccess(district, Guid.NewGuid()));
        Assert.False(scope.CanAccess(Guid.NewGuid(), phc));
    }

    [Fact]
    public void DistrictAdministratorCanAccessOnlyTheirDistrict()
    {
        var district = Guid.NewGuid();
        var scope = new UserScope(district, null, false);

        Assert.True(scope.CanAccess(district, Guid.NewGuid()));
        Assert.False(scope.CanAccess(Guid.NewGuid(), Guid.NewGuid()));
    }
}