using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.StudentPortal;

public class StudentHomeLocationViewModel
{
    [Required]
    public decimal Latitude { get; set; }

    [Required]
    public decimal Longitude { get; set; }
}
