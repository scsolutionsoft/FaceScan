using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.Models.Entities;

public class TeacherFaceProfile : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.NotRegistered;
    public string TemplateVersion { get; set; } = "v1";
    public DateTime? LastTrainedAt { get; set; }
    public string? EmbeddingJson { get; set; }
    public string? QualityNote { get; set; }

    public ApplicationUser? User { get; set; }
}
