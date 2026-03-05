using Microsoft.EntityFrameworkCore;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using KidsLearningPlatform.Api.DTOs.Admin;

namespace KidsLearningPlatform.Api.Services;

public interface IMaterialService
{
    Task<IEnumerable<MaterialDto>> GetAllMaterialsAsync();
    Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request);
    Task<bool> DeleteMaterialAsync(int id);
}

public class MaterialService : IMaterialService
{
    private readonly AppDbContext _context;

    public MaterialService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<MaterialDto>> GetAllMaterialsAsync()
    {
        return await _context.Materials
            .Include(m => m.Course)
            .Select(m => new MaterialDto
            {
                Id = m.Id,
                Name = m.Name,
                Type = m.Type,
                CourseId = m.CourseId,
                CourseTitle = m.Course.Title,
                Url = m.Url,
                Size = m.Size,
                UploadDate = m.UploadDate
            })
            .ToListAsync();
    }

    public async Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request)
    {
        var material = new Material
        {
            Name = request.Name,
            Type = request.Type,
            CourseId = request.CourseId,
            Url = request.Url,
            Size = request.Size,
            UploadDate = DateTime.UtcNow
        };
        _context.Materials.Add(material);
        await _context.SaveChangesAsync();

        var courseTitle = await _context.Courses
            .Where(c => c.Id == request.CourseId)
            .Select(c => c.Title)
            .FirstOrDefaultAsync() ?? "Unknown Course";

        return new MaterialDto
        {
            Id = material.Id,
            Name = material.Name,
            Type = material.Type,
            CourseId = material.CourseId,
            CourseTitle = courseTitle,
            Url = material.Url,
            Size = material.Size,
            UploadDate = material.UploadDate
        };
    }

    public async Task<bool> DeleteMaterialAsync(int id)
    {
        var material = await _context.Materials.FindAsync(id);
        if (material == null) return false;

        _context.Materials.Remove(material);
        await _context.SaveChangesAsync();
        return true;
    }
}
