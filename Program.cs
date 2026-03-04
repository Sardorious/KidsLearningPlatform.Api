using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Endpoints;
using KidsLearningPlatform.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ILessonService, LessonService>();
builder.Services.AddScoped<IUserService, UserService>();

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "super_secret_key_that_should_be_long_enough_for_hmac_sha256";
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
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
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
app.MapUserEndpoints();
app.MapFileEndpoints();

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

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
