namespace KidsLearningPlatform.Api.Models;

public class LessonQuestion
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]"; // JSON array of option strings
    public string CorrectAnswer { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
