namespace FaceScan.Web.Models.Options;

public class UploadSettings
{
    public int MaxFileSizeMb { get; set; } = 12;
    public string[] AllowedImageTypes { get; set; } = ["image/jpeg", "image/png", "image/webp", "image/jpg", "image/pjpeg"];
    public string[] AllowedImageExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];
}
