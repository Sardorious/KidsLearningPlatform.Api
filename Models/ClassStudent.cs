namespace KidsLearningPlatform.Api.Models;

public class ClassStudent
{
    public int ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public int StudentId { get; set; }
    public User Student { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
