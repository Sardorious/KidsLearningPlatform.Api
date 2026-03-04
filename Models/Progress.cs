namespace KidsLearningPlatform.Api.Models;

public class Progress
{
    public int Id { get; set; }
    
    public int StudentId { get; set; }
    public User Student { get; set; }

    public int LessonId { get; set; }
    public Lesson Lesson { get; set; }

    public bool IsCompleted { get; set; }
    public int Score { get; set; }
    public DateTime? CompletedAt { get; set; }
}
