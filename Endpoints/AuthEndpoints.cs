namespace KidsLearningPlatform.Api.Endpoints;

using KidsLearningPlatform.Api.DTOs.Auth;
using KidsLearningPlatform.Api.Services;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", async (RegisterRequest request, IAuthService authService) =>
        {
            var result = await authService.RegisterAsync(request);
            if (result == null) return Results.BadRequest("User with this phone number already exists.");
            return Results.Ok(result);
        });

        group.MapPost("/login", async (LoginRequest request, IAuthService authService) =>
        {
            var result = await authService.LoginAsync(request);
            if (result == null) return Results.BadRequest("Invalid phone number or password.");
            return Results.Ok(result);
        });

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAuthService authService) =>
        {
            var success = await authService.ForgotPasswordAsync(request);
            if (!success) return Results.NotFound("User not found.");
            return Results.Ok(new { message = "Password reset OTP sent to phone successfully." });
        });

        group.MapPost("/reset-password", async (ResetPasswordRequest request, IAuthService authService) =>
        {
            var success = await authService.ResetPasswordAsync(request);
            if (!success) return Results.BadRequest("Invalid or expired OTP.");
            return Results.Ok(new { message = "Password reset successfully." });
        });
    }
}
