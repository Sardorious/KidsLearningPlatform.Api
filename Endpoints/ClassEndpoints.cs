using KidsLearningPlatform.Api.DTOs.Admin;
using KidsLearningPlatform.Api.Services;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KidsLearningPlatform.Api.Endpoints;

public static class ClassEndpoints
{
    public static void MapClassEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/admin/classes").WithTags("Admin Classes").RequireAuthorization(p => p.RequireRole("ADMIN", "TEACHER"));

        group.MapGet("/", async (IClassService classService) =>
            Results.Ok(await classService.GetAllClassesAsync()));

        group.MapPost("/", async (CreateClassRequest request, IClassService classService) =>
            Results.Ok(await classService.CreateClassAsync(request)));

        group.MapDelete("/{id:int}", async (int id, IClassService classService) =>
        {
            var success = await classService.DeleteClassAsync(id);
            return success ? Results.NoContent() : Results.NotFound();
        });

        // Get students in a class
        group.MapGet("/{id:int}/students", async (int id, AppDbContext db) =>
        {
            var students = await db.ClassStudents
                .Include(cs => cs.Student)
                .Where(cs => cs.ClassId == id)
                .Select(cs => new
                {
                    cs.Student.Id,
                    cs.Student.Name,
                    cs.Student.PhoneNumber,
                    cs.JoinedAt
                })
                .ToListAsync();

            return Results.Ok(students);
        });

        // Add a student to a class
        group.MapPost("/{id:int}/students", async (int id, AddStudentToClassRequest req, AppDbContext db) =>
        {
            var classExists = await db.Classes.AnyAsync(c => c.Id == id);
            if (!classExists) return Results.NotFound("Class not found.");

            var studentExists = await db.Users.AnyAsync(u => u.Id == req.StudentId && u.Role == UserRole.STUDENT);
            if (!studentExists) return Results.NotFound("Student not found.");

            var alreadyMember = await db.ClassStudents.AnyAsync(cs => cs.ClassId == id && cs.StudentId == req.StudentId);
            if (alreadyMember) return Results.Ok(new { message = "Student already in class." });

            db.ClassStudents.Add(new ClassStudent { ClassId = id, StudentId = req.StudentId });
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Student added to class." });
        });

        // Remove a student from a class
        group.MapDelete("/{id:int}/students/{studentId:int}", async (int id, int studentId, AppDbContext db) =>
        {
            var member = await db.ClassStudents.FirstOrDefaultAsync(cs => cs.ClassId == id && cs.StudentId == studentId);
            if (member == null) return Results.NotFound();
            db.ClassStudents.Remove(member);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
