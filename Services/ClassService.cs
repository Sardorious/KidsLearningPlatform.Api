using Microsoft.EntityFrameworkCore;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using KidsLearningPlatform.Api.DTOs.Admin;

namespace KidsLearningPlatform.Api.Services;

public interface IClassService
{
    Task<IEnumerable<ClassDto>> GetAllClassesAsync();
    Task<ClassDto> CreateClassAsync(CreateClassRequest request);
    Task<bool> DeleteClassAsync(int id);
}

public class ClassService : IClassService
{
    private readonly AppDbContext _context;

    public ClassService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ClassDto>> GetAllClassesAsync()
    {
        return await _context.Classes
            .Include(c => c.Teacher)
            .Select(c => new ClassDto
            {
                Id = c.Id,
                Name = c.Name,
                Grade = c.Grade,
                TeacherId = c.TeacherId,
                TeacherName = c.Teacher.Name,
                Schedule = c.Schedule,
                Room = c.Room,
                StudentCount = c.StudentCount
            })
            .ToListAsync();
    }

    public async Task<ClassDto> CreateClassAsync(CreateClassRequest request)
    {
        var classItem = new Class
        {
            Name = request.Name,
            Grade = request.Grade,
            TeacherId = request.TeacherId,
            Schedule = request.Schedule,
            Room = request.Room,
            StudentCount = 0
        };
        _context.Classes.Add(classItem);
        await _context.SaveChangesAsync();

        var teacherName = await _context.Users
            .Where(u => u.Id == request.TeacherId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync() ?? "Unknown";

        return new ClassDto
        {
            Id = classItem.Id,
            Name = classItem.Name,
            Grade = classItem.Grade,
            TeacherId = classItem.TeacherId,
            TeacherName = teacherName,
            Schedule = classItem.Schedule,
            Room = classItem.Room,
            StudentCount = classItem.StudentCount
        };
    }

    public async Task<bool> DeleteClassAsync(int id)
    {
        var classItem = await _context.Classes.FindAsync(id);
        if (classItem == null) return false;

        _context.Classes.Remove(classItem);
        await _context.SaveChangesAsync();
        return true;
    }
}
