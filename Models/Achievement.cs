namespace KidsLearningPlatform.Api.Models;

public enum BadgeType
{
    FirstLesson,
    QuizMaster,
    CourseChampion,
    SevenDayStreak
}

public class Achievement
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public BadgeType BadgeType { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}
