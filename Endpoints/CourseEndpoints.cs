namespace KidsLearningPlatform.Api.Endpoints;

using KidsLearningPlatform.Api.DTOs.Courses;
using KidsLearningPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

public static class CourseEndpoints
{
    public static void MapCourseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/courses").WithTags("Courses");
        var adminGroup = routes.MapGroup("/api/admin/courses").WithTags("Admin Courses").RequireAuthorization(policy => policy.RequireRole("ADMIN", "TEACHER"));

        // Public
        group.MapGet("/", async (ICourseService courseService) =>
        {
            return Results.Ok(await courseService.GetAllCoursesAsync());
        });

        // Requires Auth to see full details and lessons
        group.MapGet("/{id:int}", async (int id, ICourseService courseService) =>
        {
            var course = await courseService.GetCourseByIdAsync(id);
            if (course == null) return Results.NotFound();
            return Results.Ok(course);
        }).RequireAuthorization();

        group.MapGet("/teacher", async (ICourseService courseService, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int teacherId)) return Results.Unauthorized();

            return Results.Ok(await courseService.GetCoursesByTeacherIdAsync(teacherId));
        }).RequireAuthorization(policy => policy.RequireRole("TEACHER", "ADMIN"));

        // Admin/Teacher endpoints
        adminGroup.MapPost("/", async (CreateCourseRequest request, ICourseService courseService, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int teacherId)) return Results.Unauthorized();

            var course = await courseService.CreateCourseAsync(request, teacherId);
            return Results.Created($"/api/courses/{course.Id}", course);
        });

        adminGroup.MapPut("/{id:int}", async (int id, UpdateCourseRequest request, ICourseService courseService) =>
        {
            var course = await courseService.UpdateCourseAsync(id, request);
            if (course == null) return Results.NotFound();
            return Results.Ok(course);
        });

        adminGroup.MapDelete("/{id:int}", async (int id, ICourseService courseService) =>
        {
            var success = await courseService.DeleteCourseAsync(id);
            if (!success) return Results.NotFound();
            return Results.NoContent();
        });
    }
}
