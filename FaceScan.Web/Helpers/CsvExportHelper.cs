using CsvHelper;
using System.Globalization;

namespace FaceScan.Web.Helpers;

public static class CsvExportHelper
{
    public static byte[] Export<T>(IEnumerable<T> records)
    {
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(records);
        return System.Text.Encoding.UTF8.GetBytes(writer.ToString());
    }
}
