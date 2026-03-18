using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Endpoints;
using KidsLearningPlatform.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to allow large file uploads (up to 200MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 209715200; // 200MB
});

// Configure FormOptions to allow large multipart bodies
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 209715200; // 200MB
});

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JSON options to handle Enums as strings
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Add Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ILessonService, LessonService>();
builder.Services.AddScoped<ILessonQuestionService, LessonQuestionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();
builder.Services.AddScoped<IAiService, AiService>();

// Add Memory Cache for caching AI responses
builder.Services.AddMemoryCache();

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"]
    ?? "super_secret_key_that_should_be_long_enough_for_hmac_sha256";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });
builder.Services.AddAuthorization();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:5175", "http://localhost:5176")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Seed Admin User Endpoint (Dev Only)
    app.MapPost("/api/dev/seed-admin", async (KidsLearningPlatform.Api.Data.AppDbContext context) =>
    {
        var adminExists = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(context.Users, u => u.PhoneNumber == "admin");
        if (!adminExists)
        {
            context.Users.Add(new KidsLearningPlatform.Api.Models.User
            {
                Name = "Admin Default",
                PhoneNumber = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = KidsLearningPlatform.Api.Models.UserRole.ADMIN,
                IsPhoneVerified = true
            });
            await context.SaveChangesAsync();
            return Results.Ok("Admin user created (Phone: admin, Password: admin123)");
        }
        return Results.Ok("Admin user already exists.");
    }).WithTags("Dev");
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Allow serving static files from wwwroot
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCourseEndpoints();
app.MapLessonEndpoints();
app.MapLessonQuestionEndpoints();
app.MapUserEndpoints();
app.MapFileEndpoints();
app.MapClassEndpoints();
app.MapMaterialEndpoints();
app.MapEnrollmentEndpoints();
app.MapAnnouncementEndpoints();

// ─── AI Endpoints ─────────────────────────────────────────────────────────

// POST /ai/tutor-chat — AI tutor for students
app.MapPost("/ai/tutor-chat", async (
    HttpContext ctx,
    IAiService aiService) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<TutorChatRequest>();
    if (body == null || string.IsNullOrWhiteSpace(body.Message))
        return Results.BadRequest("Message is required.");

    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");

    var stream = aiService.ChatWithTutorStreamAsync(body.Subject ?? "General", body.Message, body.Grade ?? "Grade 3");

    await foreach (var chunk in stream)
    {
        var data = $"data: {chunk.Replace("\n", "\\n")}\n\n";
        await ctx.Response.WriteAsync(data);
        await ctx.Response.Body.FlushAsync();
    }

    await ctx.Response.WriteAsync("data: [DONE]\n\n");
    await ctx.Response.Body.FlushAsync();
    
    return Results.Empty;
}).RequireAuthorization().WithTags("AI");

// POST /ai/check-writing — writing checker for teachers
app.MapPost("/ai/check-writing", async (
    HttpContext ctx,
    IAiService aiService) =>
{
    if (!ctx.Request.HasFormContentType)
        return Results.BadRequest("Expected multipart form data.");

    var form = await ctx.Request.ReadFormAsync();
    string? text = form["text"];
    string grade = form["grade"].ToString() ?? "Grade 3";
    var imageFile = form.Files.GetFile("image");

    if (string.IsNullOrWhiteSpace(text) && imageFile == null)
        return Results.BadRequest("Either text or an image is required.");

    var result = await aiService.CheckWritingAsync(text, grade, imageFile);
    return Results.Ok(result);
}).RequireAuthorization().WithTags("AI")
  .DisableAntiforgery();

// POST /ai/check-speaking — speaking/audio checker for teachers
app.MapPost("/ai/check-speaking", async (
    HttpContext ctx,
    IAiService aiService) =>
{
    if (!ctx.Request.HasFormContentType)
        return Results.BadRequest("Expected multipart form data.");
    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("audio");
    if (file == null)
        return Results.BadRequest("Audio file named 'audio' is required.");
    var result = await aiService.CheckSpeakingAsync(file);
    return Results.Ok(result);
}).RequireAuthorization().WithTags("AI")
  .DisableAntiforgery();

// POST /ai/lesson-plan — lesson plan generator for teachers
app.MapPost("/ai/lesson-plan", async (
    HttpContext ctx,
    IAiService aiService) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<LessonPlanRequest>();
    if (body == null || string.IsNullOrWhiteSpace(body.Topic))
        return Results.BadRequest("Topic is required.");
    var result = await aiService.GenerateLessonPlanAsync(body.Topic, body.AgeGroup, body.Level ?? "Beginner");
    return Results.Ok(result);
}).RequireAuthorization().WithTags("AI");

// GET /ai/progress-report — AI progress report for parents/teachers
app.MapGet("/ai/progress-report", async (
    HttpContext ctx,
    KidsLearningPlatform.Api.Data.AppDbContext db,
    IAiService aiService) =>
{
    var userIdClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdClaim, out int userId))
        return Results.Unauthorized();

    var user = await db.Users.FindAsync(userId);
    if (user == null) return Results.NotFound();

    var completedLessons = await db.Progresses.CountAsync(p => p.StudentId == userId && p.IsCompleted);
    var recentActivity = await db.Progresses
        .Where(p => p.StudentId == userId)
        .OrderByDescending(p => p.CompletedAt)
        .Take(5)
        .Select(p => p.Lesson != null ? p.Lesson.Title : "Lesson")
        .ToListAsync();

    var result = await aiService.GenerateProgressReportAsync(
        user.Name ?? "Student",
        completedLessons,
        user.XP,
        user.Coins,
        string.Join(", ", recentActivity)
    );
    return Results.Ok(result);
}).RequireAuthorization().WithTags("AI");


var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.MapControllers(); // Added for TestAiController

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// ─── AI Request DTOs ─────────────────────────────────────────────────────────
record TutorChatRequest(string Message, string? Subject, string? Grade);
record WritingCheckRequest(string Text, string? Grade);
record LessonPlanRequest(string Topic, int AgeGroup, string? Level);
