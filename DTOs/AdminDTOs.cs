namespace KidsLearningPlatform.Api.DTOs.Admin;

public class CreateClassRequest
{
    public string Name { get; set; }
    public string Grade { get; set; }
    public int TeacherId { get; set; }
    public string Schedule { get; set; }
    public string Room { get; set; }
}

public class ClassDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Grade { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; }
    public string Schedule { get; set; }
    public string Room { get; set; }
    public int StudentCount { get; set; }
}

public class CreateMaterialRequest
{
    public string Name { get; set; }
    public string Type { get; set; }
    public int CourseId { get; set; }
    public string Url { get; set; }
    public string Size { get; set; }
}

public class MaterialDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; }
    public string Url { get; set; }
    public string Size { get; set; }
    public DateTime UploadDate { get; set; }
}

public class AddStudentToClassRequest
{
    public int StudentId { get; set; }
}

