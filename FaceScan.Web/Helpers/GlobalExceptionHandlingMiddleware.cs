using FaceScan.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Helpers;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request canceled by client. Path: {Path}", context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await TryPersistErrorLogAsync(context, ex);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Cannot write error response because headers already started. Path: {Path}", context.Request.Path);
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("เกิดข้อผิดพลาดภายในระบบ กรุณาติดต่อผู้ดูแลระบบ");
        }
    }

    private async Task TryPersistErrorLogAsync(HttpContext context, Exception ex)
    {
        try
        {
            using var scope = context.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetService<FaceScan.Web.Data.ApplicationDbContext>();
            if (db is null)
            {
                return;
            }

            db.ErrorLogs.Add(new ErrorLog
            {
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                Source = ex.Source,
                LoggedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation while persisting diagnostic logs.
        }
        catch (DbUpdateException logEx)
        {
            _logger.LogWarning(logEx, "Failed to persist error log to database.");
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Unexpected error while persisting error log.");
        }
    }
}
