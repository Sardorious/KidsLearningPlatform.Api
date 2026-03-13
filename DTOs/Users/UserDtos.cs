namespace KidsLearningPlatform.Api.DTOs.Users;

public class UserProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public int XP { get; set; }
    public int Coins { get; set; }
}

public class ProgressDto
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int Score { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CompleteLessonRequest
{
    public int LessonId { get; set; }
    public int Score { get; set; }
    public int TimeSpentSeconds { get; set; } = 0;
}

public class CompleteLessonResponse
{
    public int XPEarned { get; set; }
    public int CoinsEarned { get; set; }
    public int TotalXP { get; set; }
    public int TotalCoins { get; set; }
    public List<string> NewBadges { get; set; } = new();
}

public class LeaderboardEntryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int XP { get; set; }
    public int Coins { get; set; }
}

public class AchievementDto
{
    public string BadgeType { get; set; } = string.Empty;
    public DateTime EarnedAt { get; set; }
}

public class ChildProgressDto
{
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public int XP { get; set; }
    public int Coins { get; set; }
    public int CompletedLessons { get; set; }
    public IEnumerable<ProgressDto> RecentProgress { get; set; } = new List<ProgressDto>();
}

public class CourseProgressSummaryDto
{
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int TotalLessons { get; set; }
    public int CompletedLessons { get; set; }
    public double CompletionPercent { get; set; }
    public int TotalTimeSpentSeconds { get; set; }
}
