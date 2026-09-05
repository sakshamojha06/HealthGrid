namespace HealthGrid.Application.Abstractions;

public sealed record UserScope(Guid? DistrictId, Guid? PhcId, bool IsSystemAdministrator)
{
    public bool CanAccess(Guid districtId, Guid phcId) => IsSystemAdministrator ||
        DistrictId == districtId && (PhcId is null || PhcId == phcId);
}

public interface IUserScope
{
    UserScope Current { get; }
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
