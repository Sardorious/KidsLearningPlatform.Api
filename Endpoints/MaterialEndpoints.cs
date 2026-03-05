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
            Results.Ok(await materialService.CreateMaterialAsync(request)));

        group.MapDelete("/{id:int}", async (int id, IMaterialService materialService) => 
        {
            var success = await materialService.DeleteMaterialAsync(id);
            return success ? Results.NoContent() : Results.NotFound();
        });
    }
}
