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
    public DateTime? CompletedAt { get; set; }
}

public class CompleteLessonRequest
{
    public int LessonId { get; set; }
    public int Score { get; set; }
}

public class CompleteLessonResponse
{
    public int XPEarned { get; set; }
    public int CoinsEarned { get; set; }
    public int TotalXP { get; set; }
    public int TotalCoins { get; set; }
}
