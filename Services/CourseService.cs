namespace KidsLearningPlatform.Api.Services;

using Microsoft.EntityFrameworkCore;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using KidsLearningPlatform.Api.DTOs.Courses;
using KidsLearningPlatform.Api.DTOs.Admin;

public interface ICourseService
{
    Task<IEnumerable<CourseDto>> GetAllCoursesAsync(string? search = null, string? category = null);
    Task<CourseDetailsDto?> GetCourseByIdAsync(int id);
    Task<IEnumerable<CourseDto>> GetCoursesByTeacherIdAsync(int teacherId);
    Task<CourseDto> CreateCourseAsync(CreateCourseRequest request, int teacherId);
    Task<CourseDto?> UpdateCourseAsync(int id, UpdateCourseRequest request);
    Task<bool> DeleteCourseAsync(int id);
}

public class CourseService : ICourseService
{
    private readonly AppDbContext _context;

    public CourseService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CourseDto>> GetAllCoursesAsync(string? search = null, string? category = null)
    {
        var query = _context.Courses.Include(c => c.Materials).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Title.ToLower().Contains(search.ToLower()) || c.Description.ToLower().Contains(search.ToLower()));

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category.ToLower() == category.ToLower());

        return await query.Select(c => new CourseDto
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            Category = c.Category,
            TeacherId = c.TeacherId,
            Price = c.Price,
            ImageUrl = c.ImageUrl,
            Materials = c.Materials.Select(m => new MaterialDto
            {
                Id = m.Id,
                Name = m.Name,
                Type = m.Type,
                CourseId = m.CourseId,
                Url = m.Url,
                Size = m.Size,
                UploadDate = m.UploadDate
            }).ToList()
        }).ToListAsync();
    }

    public async Task<IEnumerable<CourseDto>> GetCoursesByTeacherIdAsync(int teacherId)
    {
        return await _context.Courses
            .Where(c => c.TeacherId == teacherId)
            .Include(c => c.Materials)
            .Select(c => new CourseDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                Category = c.Category,
                TeacherId = c.TeacherId,
                Price = c.Price,
                ImageUrl = c.ImageUrl,
                Materials = c.Materials.Select(m => new MaterialDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Type = m.Type,
                    CourseId = m.CourseId,
                    Url = m.Url,
                    Size = m.Size,
                    UploadDate = m.UploadDate
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<CourseDetailsDto?> GetCourseByIdAsync(int id)
    {
        var course = await _context.Courses
            .Include(c => c.Lessons.OrderBy(l => l.OrderIndex))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (course == null) return null;

        return new CourseDetailsDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Category = course.Category,
            TeacherId = course.TeacherId,
            Price = course.Price,
            ImageUrl = course.ImageUrl,
            Lessons = course.Lessons.Select(l => new LessonSummaryDto
            {
                Id = l.Id,
                Title = l.Title,
                OrderIndex = l.OrderIndex
            }).ToList()
        };
    }

    public async Task<CourseDto> CreateCourseAsync(CreateCourseRequest request, int teacherId)
    {
        var course = new Course
        {
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Price = request.Price,
            ImageUrl = request.ImageUrl,
            TeacherId = teacherId
        };
        _context.Courses.Add(course);
        await _context.SaveChangesAsync();

        return new CourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Category = course.Category,
            TeacherId = course.TeacherId,
            Price = course.Price,
            ImageUrl = course.ImageUrl,
            Materials = new List<MaterialDto>()
        };
    }

    public async Task<CourseDto?> UpdateCourseAsync(int id, UpdateCourseRequest request)
    {
        var course = await _context.Courses
            .Include(c => c.Materials)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course == null) return null;

        course.Title = request.Title;
        course.Description = request.Description;
        course.Category = request.Category;
        course.Price = request.Price;
        course.ImageUrl = request.ImageUrl;

        await _context.SaveChangesAsync();

        return new CourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            Category = course.Category,
            TeacherId = course.TeacherId,
            Price = course.Price,
            ImageUrl = course.ImageUrl,
            Materials = course.Materials.Select(m => new MaterialDto
            {
                Id = m.Id,
                Name = m.Name,
                Type = m.Type,
                CourseId = m.CourseId,
                Url = m.Url,
                Size = m.Size,
                UploadDate = m.UploadDate
            }).ToList()
        };
    }

    public async Task<bool> DeleteCourseAsync(int id)
    {
        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course == null) return false;

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync();
        return true;
    }
}
