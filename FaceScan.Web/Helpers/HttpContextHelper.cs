namespace FaceScan.Web.Helpers;

public static class HttpContextHelper
{
    public static string GetIpAddress(HttpContext? context)
    {
        if (context is null)
        {
            return string.Empty;
        }

        var xff = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xff))
        {
            return xff;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }
}
