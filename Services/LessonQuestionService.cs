namespace KidsLearningPlatform.Api.Services;

using Microsoft.EntityFrameworkCore;
using KidsLearningPlatform.Api.Data;
using KidsLearningPlatform.Api.Models;
using KidsLearningPlatform.Api.DTOs.LessonQuestions;

public interface ILessonQuestionService
{
    Task<IEnumerable<LessonQuestionDto>> GetByLessonIdAsync(int lessonId);
    Task<LessonQuestionDto?> CreateAsync(CreateLessonQuestionRequest request);
    Task<LessonQuestionDto?> UpdateAsync(int id, UpdateLessonQuestionRequest request);
    Task<bool> DeleteAsync(int id);
}

public class LessonQuestionService : ILessonQuestionService
{
    private readonly AppDbContext _context;

    public LessonQuestionService(AppDbContext context)
    {
        _context = context;
    }

    private static LessonQuestionDto MapToDto(LessonQuestion q) => new LessonQuestionDto
    {
        Id = q.Id,
        LessonId = q.LessonId,
        QuestionText = q.QuestionText,
        OptionsJson = q.OptionsJson,
        CorrectAnswer = q.CorrectAnswer,
        OrderIndex = q.OrderIndex
    };

    public async Task<IEnumerable<LessonQuestionDto>> GetByLessonIdAsync(int lessonId)
    {
        return await _context.LessonQuestions
            .Where(q => q.LessonId == lessonId)
            .OrderBy(q => q.OrderIndex)
            .Select(q => new LessonQuestionDto
            {
                Id = q.Id,
                LessonId = q.LessonId,
                QuestionText = q.QuestionText,
                OptionsJson = q.OptionsJson,
                CorrectAnswer = q.CorrectAnswer,
                OrderIndex = q.OrderIndex
            })
            .ToListAsync();
    }

    public async Task<LessonQuestionDto?> CreateAsync(CreateLessonQuestionRequest request)
    {
        var lessonExists = await _context.Lessons.AnyAsync(l => l.Id == request.LessonId);
        if (!lessonExists) return null;

        var question = new LessonQuestion
        {
            LessonId = request.LessonId,
            QuestionText = request.QuestionText,
            OptionsJson = request.OptionsJson,
            CorrectAnswer = request.CorrectAnswer,
            OrderIndex = request.OrderIndex
        };

        _context.LessonQuestions.Add(question);
        await _context.SaveChangesAsync();
        return MapToDto(question);
    }

    public async Task<LessonQuestionDto?> UpdateAsync(int id, UpdateLessonQuestionRequest request)
    {
        var question = await _context.LessonQuestions.FirstOrDefaultAsync(q => q.Id == id);
        if (question == null) return null;

        question.QuestionText = request.QuestionText;
        question.OptionsJson = request.OptionsJson;
        question.CorrectAnswer = request.CorrectAnswer;
        question.OrderIndex = request.OrderIndex;

        await _context.SaveChangesAsync();
        return MapToDto(question);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var question = await _context.LessonQuestions.FirstOrDefaultAsync(q => q.Id == id);
        if (question == null) return false;

        _context.LessonQuestions.Remove(question);
        await _context.SaveChangesAsync();
        return true;
    }
}
