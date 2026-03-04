namespace KidsLearningPlatform.Api.Endpoints;

using KidsLearningPlatform.Api.DTOs.Users;
using KidsLearningPlatform.Api.Services;
using System.Security.Claims;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").WithTags("Users").RequireAuthorization();
        var progressGroup = routes.MapGroup("/api/progress").WithTags("Progress").RequireAuthorization();

        // User Profile
        group.MapGet("/me", async (IUserService userService, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var profile = await userService.GetUserProfileAsync(userId);
            if (profile == null) return Results.NotFound();
            return Results.Ok(profile);
        });

        // User Progress
        progressGroup.MapGet("/", async (int? courseId, IUserService userService, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var progressList = await userService.GetUserProgressAsync(userId, courseId);
            return Results.Ok(progressList);
        });

        // Complete Lesson
        progressGroup.MapPost("/complete-lesson", async (CompleteLessonRequest request, IUserService userService, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var result = await userService.CompleteLessonAsync(userId, request);
            if (result == null) return Results.BadRequest("Invalid lesson ID.");
            return Results.Ok(result);
        });
    }
}
