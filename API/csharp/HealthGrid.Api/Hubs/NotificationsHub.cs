using HealthGrid.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HealthGrid.Api.Hubs;

/// <summary>
/// Real-time operational notifications. Group membership is derived from the
/// caller's JWT claims — never from client-supplied ids — so a Ranchi user can
/// only ever join <c>district-{ranchi}</c> / <c>phc-{theirPhc}</c>.
///
/// Client-listened events: <c>notification</c>, <c>medicineRequest</c>, <c>aiAlert</c>.
/// </summary>
[Authorize]
public sealed class NotificationsHub(ICurrentUser currentUser) : Hub
{
    public static string DistrictGroup(Guid districtId) => $"district-{districtId}";
    public static string PhcGroup(Guid phcId) => $"phc-{phcId}";
    public static string UserGroup(Guid userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        if (currentUser.UserId != Guid.Empty)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(currentUser.UserId));

        if (currentUser.DistrictId is { } districtId)
            await Groups.AddToGroupAsync(Context.ConnectionId, DistrictGroup(districtId));

        if (currentUser.PhcId is { } phcId)
            await Groups.AddToGroupAsync(Context.ConnectionId, PhcGroup(phcId));

        await base.OnConnectedAsync();
    }
}
