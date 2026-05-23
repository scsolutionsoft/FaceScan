using Microsoft.AspNetCore.SignalR;

namespace FaceScan.Web.Hubs;

public class ScanLiveHub : Hub
{
    public const string ExecutiveGroup = "executive-live";
    public const string ScanEventMethod = "scanEvent";

    public Task JoinExecutiveDashboardAsync()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, ExecutiveGroup);
    }

    public Task LeaveExecutiveDashboardAsync()
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, ExecutiveGroup);
    }
}
