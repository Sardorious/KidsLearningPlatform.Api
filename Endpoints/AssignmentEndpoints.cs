namespace KidsLearningPlatform.Api.Endpoints;

using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public static class AssignmentEndpoints
{
    public record CreateAssignmentRequest(int CourseId, string Title, string Description, DateTime? DueDate, int MaxScore);
    public record SubmitAssignmentRequest(string SubmissionText, string? FileUrl);
    public record GradeSubmissionRequest(int Score, string? Feedback);

    public static void MapAssignmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Assignments").RequireAuthorization();

        // Get assignments for a course
        group.MapGet("/courses/{courseId:int}/assignments", async (int courseId, AppDbContext db) =>
        {
            var assignments = await db.Assignments.Where(a => a.CourseId == courseId).ToListAsync();
            return Results.Ok(assignments);
        });

        // Create assignment (Teacher)
        group.MapPost("/admin/assignments", async (CreateAssignmentRequest request, AppDbContext db) =>
        {
            var assignment = new Assignment
            {
                CourseId = request.CourseId,
                Title = request.Title,
                Description = request.Description,
                DueDate = request.DueDate,
                MaxScore = request.MaxScore > 0 ? request.MaxScore : 100
            };
            db.Assignments.Add(assignment);
            await db.SaveChangesAsync();
            return Results.Ok(assignment);
        }).RequireAuthorization(p => p.RequireRole("ADMIN", "TEACHER", "Teacher", "Admin"));

        // Get single assignment
        group.MapGet("/assignments/{id:int}", async (int id, AppDbContext db) =>
        {
            var assignment = await db.Assignments.FindAsync(id);
            if (assignment == null) return Results.NotFound();
            return Results.Ok(assignment);
        });

        // Submit assignment (Student)
        group.MapPost("/assignments/{assignmentId:int}/submit", async (int assignmentId, SubmitAssignmentRequest request, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Results.Unauthorized();

            var assignment = await db.Assignments.FindAsync(assignmentId);
            if (assignment == null) return Results.NotFound("Assignment not found");

            var existing = await db.AssignmentSubmissions.FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == userId);
            if (existing != null)
            {
                existing.SubmissionText = request.SubmissionText ?? string.Empty;
                existing.FileUrl = request.FileUrl ?? existing.FileUrl;
                existing.SubmittedAt = DateTime.UtcNow;
            }
            else
            {
                var submission = new AssignmentSubmission
                {
                    AssignmentId = assignmentId,
                    StudentId = userId,
                    SubmissionText = request.SubmissionText ?? string.Empty,
                    FileUrl = request.FileUrl,
                    SubmittedAt = DateTime.UtcNow
                };
                db.AssignmentSubmissions.Add(submission);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { Message = "Submitted successfully" });
        });

        // Get all submissions for an assignment (Teacher)
        group.MapGet("/assignments/{assignmentId:int}/submissions", async (int assignmentId, AppDbContext db) =>
        {
            var submissions = await db.AssignmentSubmissions
                .Include(s => s.Student)
                .Where(s => s.AssignmentId == assignmentId)
                .Select(s => new {
                    s.Id,
                    s.AssignmentId,
                    s.StudentId,
                    StudentName = s.Student!.Name,
                    s.SubmissionText,
                    s.FileUrl,
                    s.SubmittedAt,
                    s.Score,
                    s.Feedback
                })
                .ToListAsync();
            return Results.Ok(submissions);
        }).RequireAuthorization(p => p.RequireRole("ADMIN", "TEACHER", "Admin", "Teacher"));

        // Grade submission (Teacher)
        group.MapPost("/submissions/{submissionId:int}/grade", async (int submissionId, GradeSubmissionRequest request, AppDbContext db) =>
        {
            var submission = await db.AssignmentSubmissions.FindAsync(submissionId);
            if (submission == null) return Results.NotFound();

            submission.Score = request.Score;
            submission.Feedback = request.Feedback;
            await db.SaveChangesAsync();
            return Results.Ok(submission);
        }).RequireAuthorization(p => p.RequireRole("ADMIN", "TEACHER", "Admin", "Teacher"));
    }
}
