using FaceScan.Web.ViewModels.Students;

namespace FaceScan.Web.Validators;

public static class StudentValidator
{
    public static IReadOnlyList<string> Validate(StudentUpsertViewModel model)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(model.StudentCode))
        {
            errors.Add("StudentCode is required.");
        }

        if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName))
        {
            errors.Add("FirstName and LastName are required.");
        }

        if (model.AcademicYearId <= 0 || model.GradeLevelId <= 0 || model.ClassroomId <= 0)
        {
            errors.Add("AcademicYear, GradeLevel and Classroom are required.");
        }

        return errors;
    }
}
