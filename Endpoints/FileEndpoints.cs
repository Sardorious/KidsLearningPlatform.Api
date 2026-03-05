namespace KidsLearningPlatform.Api.Endpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public static class FileEndpoints
{
    public static void MapFileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/files").WithTags("Files").RequireAuthorization();

        group.MapPost("/upload", async (IFormFile file, HttpContext context) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file uploaded.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Construct the URL to return to the client
            var request = context.Request;
            var fileUrl = $"{request.Scheme}://{request.Host}/uploads/{fileName}";

            return Results.Ok(new { Url = fileUrl });
        })
        .DisableAntiforgery(); // Minimal APIs with IFormFile need this or Antiforgery configured
    }
}
