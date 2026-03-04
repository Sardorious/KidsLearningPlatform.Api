namespace KidsLearningPlatform.Api.Models;

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    
    public int TeacherId { get; set; }
    public User Teacher { get; set; }

    public decimal Price { get; set; }
    public string ImageUrl { get; set; }
    
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}
