namespace FaceScan.Web.ViewModels.Teacher;

public class TeacherWorkspaceViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string AssignedClassroomName { get; set; } = "-";
    public int AssignedStudentCount { get; set; }
    public DateTime Today { get; set; } = DateTime.Today;
    public bool CanManageTeacherFaces { get; set; }
    public bool CanViewTeacherReports { get; set; }
    public bool CanUseTeacherScan { get; set; } = true;
    public bool CanUsePeriodAttendance { get; set; } = true;
}
