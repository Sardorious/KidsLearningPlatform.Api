namespace KidsLearningPlatform.Api.Models;

public enum UserRole
{
    STUDENT,
    PARENT,
    TEACHER,
    ADMIN
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string PhoneNumber { get; set; }
    public bool IsPhoneVerified { get; set; }
    public string? OtpCode { get; set; } // For storing OTP temporarily
    public DateTime? OtpExpiryTime { get; set; }
    public UserRole Role { get; set; }
    public int? ParentId { get; set; } // For connecting a Student and a Parent
    public User? Parent { get; set; }
    public ICollection<User> Children { get; set; } = new List<User>();

    public int XP { get; set; } = 0;
    public int Coins { get; set; } = 0;
    
    public ICollection<Course> AuthoredCourses { get; set; } = new List<Course>();
    public ICollection<Progress> UserProgress { get; set; } = new List<Progress>();
}
