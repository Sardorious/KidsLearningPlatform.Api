namespace KidsLearningPlatform.Api.Services;

using Microsoft.EntityFrameworkCore;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using KidsLearningPlatform.Api.DTOs.Users;

public interface IUserService
{
    Task<UserProfileDto?> GetUserProfileAsync(int userId);
    Task<IEnumerable<UserProfileDto>> GetAllUsersAsync();
    Task<IEnumerable<ProgressDto>> GetUserProgressAsync(int userId, int? courseId = null);
    Task<CompleteLessonResponse?> CompleteLessonAsync(int userId, CompleteLessonRequest request);
}

public class UserService : IUserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        return new UserProfileDto
        {
            Id = user.Id,
            Name = user.Name,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Role = user.Role.ToString(),
            XP = user.XP,
            Coins = user.Coins
        };
    }

    public async Task<IEnumerable<UserProfileDto>> GetAllUsersAsync()
    {
        var users = await _context.Users.ToListAsync();
        return users.Select(user => new UserProfileDto
        {
            Id = user.Id,
            Name = user.Name,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Role = user.Role.ToString(),
            XP = user.XP,
            Coins = user.Coins
        });
    }

    public async Task<IEnumerable<ProgressDto>> GetUserProgressAsync(int userId, int? courseId = null)
    {
        var query = _context.Progresses
            .Include(p => p.Lesson)
            .Where(p => p.StudentId == userId);

        if (courseId.HasValue)
        {
            query = query.Where(p => p.Lesson.CourseId == courseId.Value);
        }

        return await query.Select(p => new ProgressDto
        {
            Id = p.Id,
            LessonId = p.LessonId,
            LessonTitle = p.Lesson.Title,
            IsCompleted = p.IsCompleted,
            Score = p.Score,
            CompletedAt = p.CompletedAt
        }).ToListAsync();
    }

    public async Task<CompleteLessonResponse?> CompleteLessonAsync(int userId, CompleteLessonRequest request)
    {
        // Check if lesson exists
        var lessonExists = await _context.Lessons.AnyAsync(l => l.Id == request.LessonId);
        if (!lessonExists) return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        // Check if progress already exists
        var progress = await _context.Progresses.FirstOrDefaultAsync(p => p.StudentId == userId && p.LessonId == request.LessonId);
        
        int xpEarned = 0;
        int coinsEarned = 0;

        if (progress == null)
        {
            // First time completing the lesson
            progress = new Progress
            {
                StudentId = userId,
                LessonId = request.LessonId,
                IsCompleted = true,
                Score = request.Score,
                CompletedAt = DateTime.UtcNow
            };
            _context.Progresses.Add(progress);

            // Give rewards only for the first time
            xpEarned = 10;
            coinsEarned = 5;
            user.XP += xpEarned;
            user.Coins += coinsEarned;
        }
        else if (!progress.IsCompleted)
        {
            // Was started, now completed
            progress.IsCompleted = true;
            progress.Score = request.Score;
            progress.CompletedAt = DateTime.UtcNow;
            
            xpEarned = 10;
            coinsEarned = 5;
            user.XP += xpEarned;
            user.Coins += coinsEarned;
        }
        else
        {
            // Already completed, just update score if it's better (optional logic, skipping for now, or just updating)
            progress.Score = Math.Max(progress.Score, request.Score);
        }

        await _context.SaveChangesAsync();

        return new CompleteLessonResponse
        {
            XPEarned = xpEarned,
            CoinsEarned = coinsEarned,
            TotalXP = user.XP,
            TotalCoins = user.Coins
        };
    }
}
