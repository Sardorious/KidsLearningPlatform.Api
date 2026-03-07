namespace KidsLearningPlatform.Api.Services;

using Microsoft.EntityFrameworkCore;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using KidsLearningPlatform.Api.DTOs.Lessons;

public interface ILessonService
{
    Task<LessonDto?> GetLessonByIdAsync(int id);
    Task<IEnumerable<LessonDto>> GetLessonsByCourseIdAsync(int courseId);
    Task<LessonDto?> CreateLessonAsync(CreateLessonRequest request);
    Task<LessonDto?> UpdateLessonAsync(int id, UpdateLessonRequest request);
    Task<bool> DeleteLessonAsync(int id);
}

public class LessonService : ILessonService
{
    private readonly AppDbContext _context;

    public LessonService(AppDbContext context)
    {
        _context = context;
    }

    private static LessonDto MapToDto(Lesson lesson) => new LessonDto
    {
        Id = lesson.Id,
        Title = lesson.Title,
        Content = lesson.Content,
        VideoUrl = lesson.VideoUrl,
        Type = lesson.Type ?? "video",
        ContentUrl = lesson.ContentUrl ?? lesson.VideoUrl ?? string.Empty,
        OrderIndex = lesson.OrderIndex,
        CourseId = lesson.CourseId
    };

    public async Task<LessonDto?> GetLessonByIdAsync(int id)
    {
        var lesson = await _context.Lessons.FirstOrDefaultAsync(l => l.Id == id);
        if (lesson == null) return null;
        return MapToDto(lesson);
    }

    public async Task<IEnumerable<LessonDto>> GetLessonsByCourseIdAsync(int courseId)
    {
        return await _context.Lessons
            .Where(l => l.CourseId == courseId)
            .OrderBy(l => l.OrderIndex)
            .Select(l => new LessonDto
            {
                Id = l.Id,
                Title = l.Title,
                Content = l.Content,
                VideoUrl = l.VideoUrl,
                Type = l.Type ?? "video",
                ContentUrl = l.ContentUrl ?? l.VideoUrl ?? string.Empty,
                OrderIndex = l.OrderIndex,
                CourseId = l.CourseId
            })
            .ToListAsync();
    }

    public async Task<LessonDto?> CreateLessonAsync(CreateLessonRequest request)
    {
        var courseExists = await _context.Courses.AnyAsync(c => c.Id == request.CourseId);
        if (!courseExists) return null;

        var lesson = new Lesson
        {
            Title = request.Title,
            Content = request.Content,
            VideoUrl = request.VideoUrl,
            Type = request.Type ?? "video",
            ContentUrl = request.ContentUrl ?? request.VideoUrl,
            OrderIndex = request.OrderIndex,
            CourseId = request.CourseId
        };

        _context.Lessons.Add(lesson);
        await _context.SaveChangesAsync();

        return MapToDto(lesson);
    }

    public async Task<LessonDto?> UpdateLessonAsync(int id, UpdateLessonRequest request)
    {
        var lesson = await _context.Lessons.FirstOrDefaultAsync(l => l.Id == id);
        if (lesson == null) return null;

        lesson.Title = request.Title;
        lesson.Content = request.Content;
        lesson.VideoUrl = request.VideoUrl;
        lesson.Type = string.IsNullOrEmpty(request.Type) ? (lesson.Type ?? "video") : request.Type;
        lesson.ContentUrl = string.IsNullOrEmpty(request.ContentUrl) ? (lesson.ContentUrl ?? request.VideoUrl) : request.ContentUrl;
        lesson.OrderIndex = request.OrderIndex;

        await _context.SaveChangesAsync();

        return MapToDto(lesson);
    }

    public async Task<bool> DeleteLessonAsync(int id)
    {
        var lesson = await _context.Lessons.FirstOrDefaultAsync(l => l.Id == id);
        if (lesson == null) return false;

        _context.Lessons.Remove(lesson);
        await _context.SaveChangesAsync();
        return true;
    }
}
