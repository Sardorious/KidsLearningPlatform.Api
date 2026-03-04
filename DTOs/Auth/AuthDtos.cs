namespace KidsLearningPlatform.Api.DTOs.Auth;

using KidsLearningPlatform.Api.Models;

public class RegisterRequest
{
    public string Name { get; set; }
    public string PhoneNumber { get; set; }
    public string Password { get; set; }
    public string? Email { get; set; }
    public UserRole Role { get; set; }
}

public class LoginRequest
{
    public string PhoneNumber { get; set; }
    public string Password { get; set; }
}

public class ForgotPasswordRequest
{
    public string PhoneNumber { get; set; }
}

public class ResetPasswordRequest
{
    public string PhoneNumber { get; set; }
    public string OtpCode { get; set; }
    public string NewPassword { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; }
    public UserDto User { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; }
}
