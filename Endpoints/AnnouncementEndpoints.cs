using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace KidsLearningPlatform.Api.Endpoints;

public record CreateAnnouncementRequest(string Title, string Body, string TargetRole = "ALL");

public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this IEndpointRouteBuilder routes)
    {
        // Admin: create announcement
        routes.MapPost("/api/admin/announcements", async (CreateAnnouncementRequest req, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int authorId)) return Results.Unauthorized();

            var validRoles = new[] { "ALL", "STUDENT", "PARENT", "TEACHER" };
            var targetRole = req.TargetRole.ToUpperInvariant();
            if (!validRoles.Contains(targetRole)) return Results.BadRequest("Invalid target role.");

            var announcement = new Announcement
            {
                Title = req.Title,
                Body = req.Body,
                TargetRole = targetRole,
                AuthorId = authorId
            };

            db.Announcements.Add(announcement);
            await db.SaveChangesAsync();
            return Results.Ok(announcement);
        })
        .RequireAuthorization(p => p.RequireRole("ADMIN"))
        .WithTags("Announcements");

        // Any user: get announcements relevant to their role
        routes.MapGet("/api/notifications/my", async (AppDbContext db, ClaimsPrincipal userPrincipal) =>
        {
            var roleClaim = userPrincipal.FindFirst(ClaimTypes.Role)?.Value?.ToUpperInvariant() ?? "STUDENT";

            var announcements = await db.Announcements
                .Where(a => a.TargetRole == "ALL" || a.TargetRole == roleClaim)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Body,
                    a.TargetRole,
                    a.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(announcements);
        })
        .RequireAuthorization()
        .WithTags("Announcements");

        // Admin: list all announcements
        routes.MapGet("/api/admin/announcements", async (AppDbContext db) =>
        {
            var announcements = await db.Announcements
                .Include(a => a.Author)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Body,
                    a.TargetRole,
                    a.CreatedAt,
                    AuthorName = a.Author.Name
                })
                .ToListAsync();

            return Results.Ok(announcements);
        })
        .RequireAuthorization(p => p.RequireRole("ADMIN"))
        .WithTags("Announcements");

        // Admin: delete announcement
        routes.MapDelete("/api/admin/announcements/{id:int}", async (int id, AppDbContext db) =>
        {
            var a = await db.Announcements.FindAsync(id);
            if (a == null) return Results.NotFound();
            db.Announcements.Remove(a);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization(p => p.RequireRole("ADMIN"))
        .WithTags("Announcements");
    }
}
