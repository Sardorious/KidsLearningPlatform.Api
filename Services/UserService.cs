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
    Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync();
    Task<ChildProgressDto?> GetChildProgressAsync(int parentId);
    Task<IEnumerable<AchievementDto>> GetUserAchievementsAsync(int userId);
    Task<CourseProgressSummaryDto?> GetCourseProgressSummaryAsync(int userId, int courseId);
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
            TimeSpentSeconds = p.TimeSpentSeconds,
            CompletedAt = p.CompletedAt
        }).ToListAsync();
    }

    public async Task<CompleteLessonResponse?> CompleteLessonAsync(int userId, CompleteLessonRequest request)
    {
        var lesson = await _context.Lessons.Include(l => l.Course).FirstOrDefaultAsync(l => l.Id == request.LessonId);
        if (lesson == null) return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        var progress = await _context.Progresses.FirstOrDefaultAsync(p => p.StudentId == userId && p.LessonId == request.LessonId);

        int xpEarned = 0;
        int coinsEarned = 0;

        if (progress == null)
        {
            progress = new Progress
            {
                StudentId = userId,
                LessonId = request.LessonId,
                IsCompleted = true,
                Score = request.Score,
                TimeSpentSeconds = request.TimeSpentSeconds,
                CompletedAt = DateTime.UtcNow
            };
            _context.Progresses.Add(progress);

            xpEarned = 10;
            coinsEarned = 5;
            user.XP += xpEarned;
            user.Coins += coinsEarned;
        }
        else if (!progress.IsCompleted)
        {
            progress.IsCompleted = true;
            progress.Score = request.Score;
            progress.TimeSpentSeconds = request.TimeSpentSeconds;
            progress.CompletedAt = DateTime.UtcNow;

            xpEarned = 10;
            coinsEarned = 5;
            user.XP += xpEarned;
            user.Coins += coinsEarned;
        }
        else
        {
            progress.Score = Math.Max(progress.Score, request.Score);
            progress.TimeSpentSeconds += request.TimeSpentSeconds;
        }

        // ── Badge Checks ─────────────────────────────────────────────────
        var newBadges = new List<BadgeType>();

        var existingBadges = await _context.Achievements.Where(a => a.UserId == userId).Select(a => a.BadgeType).ToListAsync();

        // FirstLesson badge
        if (!existingBadges.Contains(BadgeType.FirstLesson))
        {
            var completedCount = await _context.Progresses.CountAsync(p => p.StudentId == userId && p.IsCompleted);
            if (completedCount >= 1)
            {
                _context.Achievements.Add(new Achievement { UserId = userId, BadgeType = BadgeType.FirstLesson });
                newBadges.Add(BadgeType.FirstLesson);
            }
        }

        // QuizMaster: perfect score (100)
        if (!existingBadges.Contains(BadgeType.QuizMaster) && request.Score >= 100)
        {
            _context.Achievements.Add(new Achievement { UserId = userId, BadgeType = BadgeType.QuizMaster });
            newBadges.Add(BadgeType.QuizMaster);
        }

        // CourseChampion: all lessons in a course done
        if (!existingBadges.Contains(BadgeType.CourseChampion))
        {
            var totalLessons = await _context.Lessons.CountAsync(l => l.CourseId == lesson.CourseId);
            var completedLessons = await _context.Progresses.CountAsync(p => p.StudentId == userId && p.IsCompleted && p.Lesson.CourseId == lesson.CourseId);
            if (totalLessons > 0 && completedLessons >= totalLessons)
            {
                _context.Achievements.Add(new Achievement { UserId = userId, BadgeType = BadgeType.CourseChampion });
                newBadges.Add(BadgeType.CourseChampion);
            }
        }

        await _context.SaveChangesAsync();

        return new CompleteLessonResponse
        {
            XPEarned = xpEarned,
            CoinsEarned = coinsEarned,
            TotalXP = user.XP,
            TotalCoins = user.Coins,
            NewBadges = newBadges.Select(b => b.ToString()).ToList()
        };
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync()
    {
        return await _context.Users
            .Where(u => u.Role == UserRole.STUDENT)
            .OrderByDescending(u => u.XP)
            .Take(10)
            .Select(u => new LeaderboardEntryDto
            {
                Id = u.Id,
                Name = u.Name,
                XP = u.XP,
                Coins = u.Coins
            })
            .ToListAsync();
    }

    public async Task<ChildProgressDto?> GetChildProgressAsync(int parentId)
    {
        var parent = await _context.Users.FindAsync(parentId);
        if (parent == null || parent.Role != UserRole.PARENT) return null;

        var child = await _context.Users.FirstOrDefaultAsync(u => u.ParentId == parentId);
        if (child == null) return null;

        var progressList = await _context.Progresses
            .Include(p => p.Lesson).ThenInclude(l => l.Course)
            .Where(p => p.StudentId == child.Id && p.IsCompleted)
            .OrderByDescending(p => p.CompletedAt)
            .Take(10)
            .Select(p => new ProgressDto
            {
                Id = p.Id,
                LessonId = p.LessonId,
                LessonTitle = p.Lesson.Title,
                IsCompleted = p.IsCompleted,
                Score = p.Score,
                TimeSpentSeconds = p.TimeSpentSeconds,
                CompletedAt = p.CompletedAt
            })
            .ToListAsync();

        var totalXp = child.XP;
        var totalLessons = await _context.Progresses.CountAsync(p => p.StudentId == child.Id && p.IsCompleted);

        return new ChildProgressDto
        {
            ChildId = child.Id,
            ChildName = child.Name,
            XP = totalXp,
            Coins = child.Coins,
            CompletedLessons = totalLessons,
            RecentProgress = progressList
        };
    }

    public async Task<IEnumerable<AchievementDto>> GetUserAchievementsAsync(int userId)
    {
        return await _context.Achievements
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.EarnedAt)
            .Select(a => new AchievementDto
            {
                BadgeType = a.BadgeType.ToString(),
                EarnedAt = a.EarnedAt
            })
            .ToListAsync();
    }

    public async Task<CourseProgressSummaryDto?> GetCourseProgressSummaryAsync(int userId, int courseId)
    {
        var course = await _context.Courses.FindAsync(courseId);
        if (course == null) return null;

        var totalLessons = await _context.Lessons.CountAsync(l => l.CourseId == courseId);
        var completedLessons = await _context.Progresses.CountAsync(p => p.StudentId == userId && p.IsCompleted && p.Lesson.CourseId == courseId);
        var totalTime = await _context.Progresses
            .Where(p => p.StudentId == userId && p.Lesson.CourseId == courseId)
            .SumAsync(p => p.TimeSpentSeconds);

        return new CourseProgressSummaryDto
        {
            CourseId = courseId,
            CourseTitle = course.Title,
            TotalLessons = totalLessons,
            CompletedLessons = completedLessons,
            CompletionPercent = totalLessons > 0 ? Math.Round((double)completedLessons / totalLessons * 100, 1) : 0,
            TotalTimeSpentSeconds = totalTime
        };
    }
}
