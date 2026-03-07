namespace KidsLearningPlatform.Api.Models;

public class Lesson
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string VideoUrl { get; set; }
    public string Type { get; set; } = "video";
    public string ContentUrl { get; set; }
    public int OrderIndex { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; }
}
