namespace KidsLearningPlatform.Api.Models;

public class Class
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Grade { get; set; }
    public int TeacherId { get; set; }
    public User Teacher { get; set; }
    public string Schedule { get; set; }
    public string Room { get; set; }
    public int StudentCount { get; set; }
}
