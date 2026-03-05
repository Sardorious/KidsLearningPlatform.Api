namespace KidsLearningPlatform.Api.Models;

public class Material
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; } // video, document, etc.
    public int CourseId { get; set; }
    public Course Course { get; set; }
    public string Url { get; set; }
    public string Size { get; set; }
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
}
