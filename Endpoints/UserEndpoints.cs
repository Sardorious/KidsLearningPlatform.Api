namespace KidsLearningPlatform.Api.Endpoints;

using KidsLearningPlatform.Api.DTOs.Users;
using KidsLearningPlatform.Api.DTOs.Auth;
using KidsLearningPlatform.Api.Services;
using System.Security.Claims;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").WithTags("Users").RequireAuthorization();
        var progressGroup = routes.MapGroup("/api/progress").WithTags("Progress").RequireAuthorization();

        // Get All Users (Admin/Teacher)
        group.MapGet("/", async (IUserService userService, ClaimsPrincipal user) =>
        {
            var users = await userService.GetAllUsersAsync();
            return Results.Ok(users);
        }).RequireAuthorization(p => p.RequireRole("ADMIN", "TEACHER", "Admin", "Teacher"));

        // Create User (Admin)
        group.MapPost("/", async (RegisterRequest request, IAuthService authService) =>
        {
            var result = await authService.RegisterAsync(request);
            if (result == null) return Results.BadRequest("User with this phone number already exists.");
            return Results.Ok(result);
        }).RequireAuthorization(p => p.RequireRole("ADMIN"));

        // User Profile
        group.MapGet("/me", async (IUserService userService, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var profile = await userService.GetUserProfileAsync(userId);
            if (profile == null) return Results.NotFound();
            return Results.Ok(profile);
        });

        // My Achievements / Badges
        group.MapGet("/my-badges", async (IUserService userService, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Results.Unauthorized();
            var badges = await userService.GetUserAchievementsAsync(userId);
            return Results.Ok(badges);
        });

        // Child Progress (for parents)
        group.MapGet("/{childId:int}/progress", async (int childId, IUserService userService, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Results.Unauthorized();
            var progress = await userService.GetChildProgressAsync(userId, childId);
            if (progress == null) return Results.NotFound("No child linked to this account or invalid child ID.");
            return Results.Ok(progress);
        }).RequireAuthorization(p => p.RequireRole("PARENT"));

        // My Children (for parents)
        group.MapGet("/my-children", async (KidsLearningPlatform.Api.Data.AppDbContext db, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Results.Unauthorized();
            
            var children = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                db.Users.Where(u => u.ParentId == userId)
            );
            
            var result = children.Select(c => new 
            {
                c.Id,
                c.Name,
                c.XP,
                c.Coins
            });
            
            return Results.Ok(result);
        }).RequireAuthorization(p => p.RequireRole("PARENT"));

        // Leaderboard (Top 10 students by XP)
        routes.MapGet("/api/leaderboard", async (IUserService userService) =>
        {
            var leaderboard = await userService.GetLeaderboardAsync();
            return Results.Ok(leaderboard);
        }).RequireAuthorization().WithTags("Users");

        // User Progress
        progressGroup.MapGet("/", async (int? courseId, IUserService userService, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int userId)) return Results.Unauthorized();

            var progressList = await userService.GetUserProgressAsync(userId, courseId);
            return Results.Ok(progressList);
        });

        // Course Progress Summary
        progressGroup.MapGet("/course/{courseId:int}/summary", async (int courseId, IUserService userService, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Results.Unauthorized();
            var summary = await userService.GetCourseProgressSummaryAsync(userId, courseId);
            if (summary == null) return Results.NotFound();
            return Results.Ok(summary);
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
