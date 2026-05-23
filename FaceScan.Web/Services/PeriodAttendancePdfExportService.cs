using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Services.Interfaces;
using FaceScan.Web.ViewModels.Teacher;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FaceScan.Web.Services;

public class PeriodAttendancePdfExportService : IPeriodAttendancePdfExportService
{
    private const string FontFamilyName = "Noto Sans Thai";
    private static readonly object FontRegistrationLock = new();
    private static bool _fontsRegistered;
    private readonly string _fontPath;
    private readonly string _fallbackFontPath;

    public PeriodAttendancePdfExportService(IWebHostEnvironment environment)
    {
        _fontPath = Path.Combine(environment.ContentRootPath, "Resources", "Fonts", "NotoSansThai.ttf");
        _fallbackFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "tahoma.ttf");
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
        EnsureFontRegistered();
    }

    public byte[] Generate(PeriodAttendancePageViewModel model, string classroomName, string currentPeriod)
    {
        EnsureFontRegistered();

        var reportCells = model.ReportRows
            .SelectMany(row => row.Periods)
            .ToList();
        var presentCount = reportCells.Count(x => x.HasRecordedStatus && x.Status == PeriodAttendanceStatus.Present);
        var lateCount = reportCells.Count(x => x.HasRecordedStatus && x.Status == PeriodAttendanceStatus.Late);
        var absentCount = reportCells.Count(x => x.HasRecordedStatus && x.Status == PeriodAttendanceStatus.Absent);
        var leaveCount = reportCells.Count(x => x.HasRecordedStatus && x.Status == PeriodAttendanceStatus.Leave);
        var truancyCount = reportCells.Count(x => x.HasRecordedStatus && x.Status == PeriodAttendanceStatus.Truancy);
        var otherCount = reportCells.Count(x => x.HasRecordedStatus && x.Status == PeriodAttendanceStatus.Other);
        var emptyCount = reportCells.Count(x => !x.HasRecordedStatus);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(TextStyle.Default.FontFamily(FontFamilyName).FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Text("รายงานสถานะการมาเรียนรายคาบ")
                        .FontFamily(FontFamilyName)
                        .FontSize(20)
                        .SemiBold();

                    column.Item().Text($"วันที่: {model.ReportFilter.Date:dd/MM/yyyy}    ห้องเรียน: {classroomName}    คาบที่เน้น: {currentPeriod}")
                        .FontFamily(FontFamilyName)
                        .FontSize(12);

                    column.Item().Text($"สรุปสถานะ: มาเรียน {presentCount} | ลา {leaveCount} | ขาด {absentCount} | หนีเรียน {truancyCount} | มาสาย {lateCount} | อื่นๆ {otherCount} | ยังไม่มีข้อมูล {emptyCount}")
                        .FontFamily(FontFamilyName)
                        .FontSize(11);

                    column.Item().Text(text =>
                    {
                        text.Span("สัญลักษณ์: ").FontFamily(FontFamilyName).SemiBold();

                        foreach (var status in PeriodAttendanceStatusPalette.OrderedStatuses)
                        {
                            text.Span("● ").FontColor(GetStatusColor(status)).FontSize(16);
                            text.Span($"{status.GetDisplayName()}  ").FontFamily(FontFamilyName);
                        }

                        text.Span("● ").FontColor(GetStatusColor(null)).FontSize(16);
                        text.Span("ยังไม่มีข้อมูล  ").FontFamily(FontFamilyName);

                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(200);
                            foreach (var _ in model.PeriodOptions)
                            {
                                columns.ConstantColumn(48);
                            }
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(container => TableCell(container, false))
                                .Text("นักเรียน")
                                .FontFamily(FontFamilyName)
                                .SemiBold();

                            foreach (var period in model.PeriodOptions)
                            {
                                var isCurrent = model.ReportFilter.ClassPeriodId.GetValueOrDefault() > 0 && model.ReportFilter.ClassPeriodId == period.Value;
                                header.Cell().Element(container => TableCell(container, isCurrent))
                                    .AlignCenter()
                                    .AlignMiddle()
                                    .Text(period.Text)
                                    .FontFamily(FontFamilyName)
                                    .FontSize(9)
                                    .SemiBold();
                            }
                        });

                        if (model.ReportRows.Count == 0)
                        {
                            table.Cell().ColumnSpan((uint)(model.PeriodOptions.Count + 1))
                                .Element(container => TableCell(container, false))
                                .AlignCenter()
                                .Text("ยังไม่มีข้อมูลสำหรับส่งออก PDF")
                                .FontFamily(FontFamilyName);
                        }
                        else
                        {
                            foreach (var row in model.ReportRows)
                            {
                                table.Cell().Element(container => TableCell(container, false))
                                    .Column(cell =>
                                    {
                                        cell.Item().Text(row.StudentName).FontFamily(FontFamilyName).SemiBold();
                                        cell.Item().Text(row.StudentCode).FontFamily(FontFamilyName).FontSize(9).FontColor(Colors.Grey.Darken1);
                                    });

                                foreach (var cell in row.Periods)
                                {
                                    var statusColor = GetStatusColor(cell.HasRecordedStatus ? cell.Status : null);
                                    table.Cell().Element(container => TableCell(container, cell.IsCurrentPeriod))
                                        .AlignCenter()
                                        .AlignMiddle()
                                        .Text("●")
                                        .FontFamily(FontFamilyName)
                                        .FontSize(16)
                                        .FontColor(statusColor);
                                }
                            }
                        }
                    });

                    column.Item().AlignRight().Text($"สร้างไฟล์เมื่อ {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontFamily(FontFamilyName)
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }

    private void EnsureFontRegistered()
    {
        if (_fontsRegistered)
        {
            return;
        }

        lock (FontRegistrationLock)
        {
            if (_fontsRegistered)
            {
                return;
            }

            var fontSourcePath = File.Exists(_fontPath)
                ? _fontPath
                : File.Exists(_fallbackFontPath)
                    ? _fallbackFontPath
                    : null;

            if (fontSourcePath is null)
            {
                throw new FileNotFoundException("Thai PDF font file was not found.", _fontPath);
            }

            using var fontStream = File.OpenRead(fontSourcePath);
            FontManager.RegisterFontWithCustomName(FontFamilyName, fontStream);
            _fontsRegistered = true;
        }
    }

    private static IContainer TableCell(IContainer container, bool isCurrentPeriod)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(isCurrentPeriod ? "#EEF7FF" : Colors.White)
            .PaddingVertical(6)
            .PaddingHorizontal(4);
    }

    private static string GetStatusColor(PeriodAttendanceStatus? status) => status switch
    {
        PeriodAttendanceStatus.Present => "#2B9348",
        PeriodAttendanceStatus.Leave => "#F4C430",
        PeriodAttendanceStatus.Absent => "#F8961E",
        PeriodAttendanceStatus.Truancy => "#D62828",
        PeriodAttendanceStatus.Late => "#7B2CBF",
        PeriodAttendanceStatus.Other => "#4EA8DE",
        _ => "#CBD5E1"
    };
}
