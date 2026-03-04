namespace KidsLearningPlatform.Api.DTOs.Courses;

public class CourseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int TeacherId { get; set; }
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class CreateCourseRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class UpdateCourseRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class CourseDetailsDto : CourseDto
{
    public List<LessonSummaryDto> Lessons { get; set; } = new();
}

public class LessonSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}
