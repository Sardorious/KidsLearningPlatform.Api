namespace KidsLearningPlatform.Api.DTOs.LessonQuestions;

public class LessonQuestionDto
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]";
    public string CorrectAnswer { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}

public class CreateLessonQuestionRequest
{
    public int LessonId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]";
    public string CorrectAnswer { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}

public class UpdateLessonQuestionRequest
{
    public string QuestionText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]";
    public string CorrectAnswer { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
