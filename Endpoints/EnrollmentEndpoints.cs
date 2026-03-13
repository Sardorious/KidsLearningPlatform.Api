using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace KidsLearningPlatform.Api.Endpoints;

public static class EnrollmentEndpoints
{
    public static void MapEnrollmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/courses").WithTags("Enrollments").RequireAuthorization();

        // Enroll current student in a course
        group.MapPost("/{id:int}/enroll", async (int id, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Results.Unauthorized();

            var course = await db.Courses.FindAsync(id);
            if (course == null) return Results.NotFound("Course not found.");

            var existing = await db.Enrollments.AnyAsync(e => e.StudentId == userId && e.CourseId == id);
            if (existing) return Results.Ok(new { message = "Already enrolled." });

            db.Enrollments.Add(new Enrollment { StudentId = userId, CourseId = id });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Enrolled successfully." });
        });

        // Get current user's enrollments
        routes.MapGet("/api/users/my-enrollments", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Results.Unauthorized();

            var enrollments = await db.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == userId)
                .Select(e => new
                {
                    e.Id,
                    e.CourseId,
                    CourseTitle = e.Course.Title,
                    CourseImageUrl = e.Course.ImageUrl,
                    CourseCategory = e.Course.Category,
                    e.Status,
                    e.EnrolledAt
                })
                .ToListAsync();

            return Results.Ok(enrollments);
        }).RequireAuthorization().WithTags("Enrollments");

        // Admin/Teacher: get enrolled students for a course
        routes.MapGet("/api/admin/courses/{id:int}/students", async (int id, AppDbContext db) =>
        {
            var students = await db.Enrollments
                .Include(e => e.Student)
                .Where(e => e.CourseId == id)
                .Select(e => new
                {
                    e.Student.Id,
                    e.Student.Name,
                    e.Student.PhoneNumber,
                    e.Status,
                    e.EnrolledAt
                })
                .ToListAsync();

            return Results.Ok(students);
        }).RequireAuthorization(p => p.RequireRole("ADMIN", "TEACHER")).WithTags("Enrollments");
    }
}
