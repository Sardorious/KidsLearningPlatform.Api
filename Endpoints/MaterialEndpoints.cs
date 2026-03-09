using KidsLearningPlatform.Api.DTOs.Admin;
using KidsLearningPlatform.Api.DTOs.Materials;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using KidsLearningPlatform.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace KidsLearningPlatform.Api.Endpoints;

public static class MaterialEndpoints
{
    public static void MapMaterialEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/admin/materials").WithTags("Admin Materials").RequireAuthorization(p => p.RequireRole("ADMIN", "TEACHER"));

        group.MapGet("/", async (IMaterialService materialService) => 
            Results.Ok(await materialService.GetAllMaterialsAsync()));

        group.MapPost("/", async (CreateMaterialRequest request, IMaterialService materialService) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest("Material name is required.");
                if (string.IsNullOrWhiteSpace(request.Url))
                    return Results.BadRequest("File URL is required. Upload the file first.");
                if (request.CourseId <= 0)
                    return Results.BadRequest("A valid CourseId is required.");

                var result = await materialService.CreateMaterialAsync(request);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                // Surface the real error so it shows in network responses
                return Results.Problem(
                    detail: ex.InnerException?.Message ?? ex.Message,
                    title: "Failed to create material",
                    statusCode: 500);
            }
        });

        group.MapDelete("/{id:int}", async (int id, IMaterialService materialService) =>
        {
            var success = await materialService.DeleteMaterialAsync(id);
            return success ? Results.NoContent() : Results.NotFound();
        });

        // AI Question Generation Endpoints
        group.MapPost("/{id:int}/generate-questions", async (int id, GenerateQuestionsRequest request, AppDbContext db, IAiService aiService) =>
        {
            var material = await db.Materials.FindAsync(id);
            if (material == null) return Results.NotFound("Material not found.");

            var questions = await aiService.GenerateQuestionsAsync(material, request.Count);
            return Results.Ok(questions);
        });

        group.MapGet("/{id:int}/questions", async (int id, AppDbContext db) =>
        {
            var questions = await db.MaterialQuestions
                .Where(q => q.MaterialId == id)
                .OrderBy(q => q.OrderIndex)
                .Select(q => new MaterialQuestionDto
                {
                    Id = q.Id,
                    MaterialId = q.MaterialId,
                    QuestionText = q.QuestionText,
                    OptionsJson = q.OptionsJson,
                    CorrectAnswer = q.CorrectAnswer,
                    OrderIndex = q.OrderIndex
                })
                .ToListAsync();

            return Results.Ok(questions);
        });

        group.MapPost("/{id:int}/questions", async (int id, SaveQuestionsRequest request, AppDbContext db) =>
        {
            var material = await db.Materials.FindAsync(id);
            if (material == null) return Results.NotFound("Material not found.");

            // Clear existing questions for this material
            var existing = await db.MaterialQuestions.Where(q => q.MaterialId == id).ToListAsync();
            db.MaterialQuestions.RemoveRange(existing);

            // Add new approved questions
            var newQuestions = request.Questions.Select(q => new MaterialQuestion
            {
                MaterialId = id,
                QuestionText = q.QuestionText,
                OptionsJson = q.OptionsJson,
                CorrectAnswer = q.CorrectAnswer,
                OrderIndex = q.OrderIndex
            }).ToList();

            db.MaterialQuestions.AddRange(newQuestions);
            await db.SaveChangesAsync();

            return Results.Ok("Questions saved successfully.");
        });
    }
}
