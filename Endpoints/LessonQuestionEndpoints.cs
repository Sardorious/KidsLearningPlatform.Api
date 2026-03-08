namespace KidsLearningPlatform.Api.Endpoints;

using KidsLearningPlatform.Api.DTOs.LessonQuestions;
using KidsLearningPlatform.Api.Services;

public static class LessonQuestionEndpoints
{
    public static void MapLessonQuestionEndpoints(this IEndpointRouteBuilder routes)
    {
        // Public: students fetch questions for a lesson during the quiz
        var group = routes.MapGroup("/api/lesson-questions").WithTags("Lesson Questions").RequireAuthorization();

        // Admin: manage questions
        var adminGroup = routes.MapGroup("/api/admin/lesson-questions").WithTags("Admin Lesson Questions")
            .RequireAuthorization(policy => policy.RequireRole("ADMIN", "TEACHER"));

        group.MapGet("/lesson/{lessonId:int}", async (int lessonId, ILessonQuestionService service) =>
        {
            return Results.Ok(await service.GetByLessonIdAsync(lessonId));
        });

        adminGroup.MapPost("/", async (CreateLessonQuestionRequest request, ILessonQuestionService service) =>
        {
            var result = await service.CreateAsync(request);
            if (result == null) return Results.BadRequest("Lesson not found");
            return Results.Created($"/api/lesson-questions/{result.Id}", result);
        });

        adminGroup.MapPut("/{id:int}", async (int id, UpdateLessonQuestionRequest request, ILessonQuestionService service) =>
        {
            var result = await service.UpdateAsync(id, request);
            if (result == null) return Results.NotFound();
            return Results.Ok(result);
        });

        adminGroup.MapDelete("/{id:int}", async (int id, ILessonQuestionService service) =>
        {
            var success = await service.DeleteAsync(id);
            if (!success) return Results.NotFound();
            return Results.NoContent();
        });
    }
}
