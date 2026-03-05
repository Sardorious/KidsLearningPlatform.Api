using KidsLearningPlatform.Api.DTOs.Admin;
using KidsLearningPlatform.Api.Services;

namespace KidsLearningPlatform.Api.Endpoints;

public static class ClassEndpoints
{
    public static void MapClassEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/admin/classes").WithTags("Admin Classes").RequireAuthorization(p => p.RequireRole("ADMIN"));

        group.MapGet("/", async (IClassService classService) => 
            Results.Ok(await classService.GetAllClassesAsync()));

        group.MapPost("/", async (CreateClassRequest request, IClassService classService) => 
            Results.Ok(await classService.CreateClassAsync(request)));

        group.MapDelete("/{id:int}", async (int id, IClassService classService) => 
        {
            var success = await classService.DeleteClassAsync(id);
            return success ? Results.NoContent() : Results.NotFound();
        });
    }
}
