using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentHomeLocationInputViewModel
{
    [Required]
    public int StudentId { get; set; }

    [Required]
    public decimal Latitude { get; set; }

    [Required]
    public decimal Longitude { get; set; }
}
