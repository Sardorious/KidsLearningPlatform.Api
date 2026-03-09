using Microsoft.AspNetCore.Mvc;
using KidsLearningPlatform.Api.Services;
using KidsLearningPlatform.Api.DTOs.LessonQuestions;

namespace KidsLearningPlatform.Api.Controllers;

[ApiController]
public class LessonQuestionsController : ControllerBase
{
    private readonly ILessonQuestionService _service;

    public LessonQuestionsController(ILessonQuestionService service)
    {
        _service = service;
    }

    // Public: get questions for a lesson (students use this during quiz)
    [HttpGet("api/lesson-questions/lesson/{lessonId}")]
    public async Task<IActionResult> GetByLesson(int lessonId)
    {
        var questions = await _service.GetByLessonIdAsync(lessonId);
        return Ok(questions);
    }

    // Admin: create question
    [HttpPost("api/admin/lesson-questions")]
    public async Task<IActionResult> Create([FromBody] CreateLessonQuestionRequest request)
    {
        var result = await _service.CreateAsync(request);
        if (result == null) return NotFound("Lesson not found");
        return Ok(result);
    }

    // Admin: update question
    [HttpPut("api/admin/lesson-questions/{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateLessonQuestionRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    // Admin: delete question
    [HttpDelete("api/admin/lesson-questions/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }
}
