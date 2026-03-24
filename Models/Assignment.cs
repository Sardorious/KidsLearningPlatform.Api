namespace KidsLearningPlatform.Api.Models;

using System;
using System.Collections.Generic;

public class Assignment
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int MaxScore { get; set; } = 100;

    public int CourseId { get; set; }
    public Course? Course { get; set; }

    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}
