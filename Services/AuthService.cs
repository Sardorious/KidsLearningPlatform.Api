namespace KidsLearningPlatform.Api.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.DTOs.Auth;
using KidsLearningPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly string _jwtSecret;

    public AuthService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _jwtSecret = config["Jwt:Secret"] ?? "super_secret_key_that_should_be_long_enough_for_hmac_sha256";
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
            return null; // User already exists

        var user = new User
        {
            Name = request.Name,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Role = request.Role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsPhoneVerified = false // In a real system, you'd verify phone first
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return GenerateAuthResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null || user.PasswordHash == null) return null;

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid) return null;

        return GenerateAuthResponse(user);
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null) return false;

        // Generate a simple 4-digit OTP for testing
        user.OtpCode = "1234"; // Random.Shared.Next(1000, 9999).ToString()
        user.OtpExpiryTime = DateTime.UtcNow.AddMinutes(15);
        
        await _context.SaveChangesAsync();
        
        Console.WriteLine($"[SMS MOCK] Sent Password Reset OTP {user.OtpCode} to {user.PhoneNumber}");
        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        if (user == null) return false;

        if (user.OtpCode != request.OtpCode || user.OtpExpiryTime < DateTime.UtcNow)
            return false; // Invalid or expired OTP

        // OTP is valid, update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        
        // Clear OTP state
        user.OtpCode = null;
        user.OtpExpiryTime = null;
        
        await _context.SaveChangesAsync();
        return true;
    }

    private AuthResponse GenerateAuthResponse(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSecret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(ClaimTypes.Name, user.Name)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return new AuthResponse
        {
            Token = tokenHandler.WriteToken(token),
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                PhoneNumber = user.PhoneNumber,
                Email = user.Email,
                Role = user.Role.ToString()
            }
        };
    }
}
