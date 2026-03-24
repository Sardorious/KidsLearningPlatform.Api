namespace KidsLearningPlatform.Api.Models;

using System;

public class AssignmentSubmission
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }
    
    public int StudentId { get; set; }
    public User? Student { get; set; }

    public string SubmissionText { get; set; } = string.Empty;
    public string? FileUrl { get; set; } // Optional file attachment
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public int? Score { get; set; }
    public string? Feedback { get; set; }
}
