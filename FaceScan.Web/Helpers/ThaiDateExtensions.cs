using System.Globalization;

namespace FaceScan.Web.Helpers;

public static class ThaiDateExtensions
{
    private static readonly CultureInfo ThaiCulture = BuildThaiCulture();

    public static string ToThaiDate(this DateTime value, string format = "dd/MM/yyyy")
    {
        return value.ToString(format, ThaiCulture);
    }

    public static string? ToThaiDate(this DateTime? value, string format = "dd/MM/yyyy")
    {
        return value.HasValue ? value.Value.ToString(format, ThaiCulture) : null;
    }

    private static CultureInfo BuildThaiCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("th-TH").Clone();
        culture.DateTimeFormat.Calendar = new ThaiBuddhistCalendar();
        return culture;
    }
}
