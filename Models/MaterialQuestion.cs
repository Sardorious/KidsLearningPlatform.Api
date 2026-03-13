namespace KidsLearningPlatform.Api.Models;

public class MaterialQuestion
{
    public int Id { get; set; }
    
    public int MaterialId { get; set; }
    public Material Material { get; set; }
    
    public string QuestionText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = "[]"; // JSON array of option strings (4 choices)
    public string CorrectAnswer { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string QuestionType { get; set; } = "MultipleChoice"; // MultipleChoice, TrueFalse
}
