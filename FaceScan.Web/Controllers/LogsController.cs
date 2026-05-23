using FaceScan.Web.Data;
using FaceScan.Web.ViewModels.Logs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Controllers;

[Authorize(Roles = "SuperAdmin,Admin")]
public class LogsController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public LogsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] LogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = new LogIndexViewModel { Filter = filter };

        if (string.Equals(filter.Type, "Error", StringComparison.OrdinalIgnoreCase))
        {
            var query = _dbContext.ErrorLogs.AsNoTracking().OrderByDescending(x => x.LoggedAt).AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                query = query.Where(x => x.Message.Contains(filter.Search) || (x.Source != null && x.Source.Contains(filter.Search)));
            }

            model.ErrorLogs = await query.Take(200).Select(x => new ErrorLogRowViewModel
            {
                LoggedAt = x.LoggedAt,
                Message = x.Message,
                Source = x.Source
            }).ToListAsync(cancellationToken);
        }
        else
        {
            var query = _dbContext.AuditLogs.AsNoTracking().OrderByDescending(x => x.LoggedAt).AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                query = query.Where(x => x.Action.Contains(filter.Search) || x.EntityName.Contains(filter.Search) || x.Detail.Contains(filter.Search));
            }

            model.AuditLogs = await query.Take(200).Select(x => new AuditLogRowViewModel
            {
                LoggedAt = x.LoggedAt,
                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                Detail = x.Detail,
                IpAddress = x.IpAddress
            }).ToListAsync(cancellationToken);
        }

        return View(model);
    }
}
