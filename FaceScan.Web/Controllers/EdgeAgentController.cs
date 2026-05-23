using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.ViewModels.EdgeAgent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Controllers;

[AllowAnonymous]
public class EdgeAgentController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public EdgeAgentController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Heartbeat([FromForm] EdgeAgentHeartbeatRequestViewModel request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "ข้อมูล heartbeat ไม่ถูกต้อง" });
        }

        var stationCode = request.StationCode.Trim();
        var stationToken = request.StationToken.Trim();
        var agentId = request.AgentId.Trim();

        var device = await _dbContext.ScanDevices
            .FirstOrDefaultAsync(x => x.StationCode == stationCode && x.AccessToken == stationToken && x.IsActive, cancellationToken);

        if (device is null)
        {
            return Unauthorized(new { success = false, message = "stationCode หรือ stationToken ไม่ถูกต้อง" });
        }

        var nowUtc = request.CapturedAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var ip = HttpContextHelper.GetIpAddress(HttpContext);

        var heartbeat = await _dbContext.EdgeAgentHeartbeats
            .FirstOrDefaultAsync(x => x.StationCode == stationCode && x.AgentId == agentId, cancellationToken);

        if (heartbeat is null)
        {
            heartbeat = new Models.Entities.EdgeAgentHeartbeat
            {
                StationCode = stationCode,
                AgentId = agentId,
                LastSeenAtUtc = nowUtc,
                LastMessage = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
                LastIpAddress = ip
            };

            _dbContext.EdgeAgentHeartbeats.Add(heartbeat);
        }
        else
        {
            heartbeat.LastSeenAtUtc = nowUtc;
            heartbeat.LastMessage = string.IsNullOrWhiteSpace(request.Message) ? heartbeat.LastMessage : request.Message.Trim();
            heartbeat.LastIpAddress = ip;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, stationCode, agentId, lastSeenAtUtc = nowUtc });
    }
}