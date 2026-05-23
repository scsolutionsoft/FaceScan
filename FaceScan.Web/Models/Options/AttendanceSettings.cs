namespace FaceScan.Web.Models.Options;

public class AttendanceSettings
{
    public int DuplicateWindowMinutes { get; set; } = 3;
    public decimal FaceConfidenceThreshold { get; set; } = 0.80m;
    public bool SaveSnapshots { get; set; } = true;
    public string CheckOutStartTime { get; set; } = "15:30";
}
