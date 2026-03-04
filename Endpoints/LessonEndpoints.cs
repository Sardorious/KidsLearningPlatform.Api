namespace KidsLearningPlatform.Api.Endpoints;

using KidsLearningPlatform.Api.DTOs.Lessons;
using KidsLearningPlatform.Api.Services;

public static class LessonEndpoints
{
    public static void MapLessonEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/lessons").WithTags("Lessons").RequireAuthorization();
        var adminGroup = routes.MapGroup("/api/admin/lessons").WithTags("Admin Lessons").RequireAuthorization(policy => policy.RequireRole("ADMIN", "TEACHER"));

        group.MapGet("/{id:int}", async (int id, ILessonService lessonService) =>
        {
            var lesson = await lessonService.GetLessonByIdAsync(id);
            if (lesson == null) return Results.NotFound();
            return Results.Ok(lesson);
        });

        group.MapGet("/course/{courseId:int}", async (int courseId, ILessonService lessonService) =>
        {
            return Results.Ok(await lessonService.GetLessonsByCourseIdAsync(courseId));
        });

        adminGroup.MapPost("/", async (CreateLessonRequest request, ILessonService lessonService) =>
        {
            var lesson = await lessonService.CreateLessonAsync(request);
            if (lesson == null) return Results.BadRequest("Invalid course ID.");
            return Results.Created($"/api/lessons/{lesson.Id}", lesson);
        });

        adminGroup.MapPut("/{id:int}", async (int id, UpdateLessonRequest request, ILessonService lessonService) =>
        {
            var lesson = await lessonService.UpdateLessonAsync(id, request);
            if (lesson == null) return Results.NotFound();
            return Results.Ok(lesson);
        });

        adminGroup.MapDelete("/{id:int}", async (int id, ILessonService lessonService) =>
        {
            var success = await lessonService.DeleteLessonAsync(id);
            if (!success) return Results.NotFound();
            return Results.NoContent();
        });
    }
}
