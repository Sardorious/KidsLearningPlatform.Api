namespace KidsLearningPlatform.Api.Models;

public class Announcement
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    /// <summary>Target audience: ALL, STUDENT, PARENT, TEACHER</summary>
    public string TargetRole { get; set; } = "ALL";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
