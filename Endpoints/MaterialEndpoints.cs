using KidsLearningPlatform.Api.DTOs.Admin;
using KidsLearningPlatform.Api.Services;

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
    }
}
