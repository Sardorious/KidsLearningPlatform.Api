namespace KidsLearningPlatform.Api.DTOs.Materials;

public class MaterialQuestionDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]";
    public string CorrectAnswer { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}

public class GenerateQuestionsRequest
{
    public int Count { get; set; } = 5; // Default 5
}

public class SaveQuestionsRequest
{
    public List<MaterialQuestionDto> Questions { get; set; } = new();
}
