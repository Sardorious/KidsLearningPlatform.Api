using Microsoft.AspNetCore.Mvc;
using KidsLearningPlatform.Api.Services;
using KidsLearningPlatform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KidsLearningPlatform.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TestAiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly AppDbContext _db;

    public TestAiController(IAiService aiService, AppDbContext db)
    {
        _aiService = aiService;
        _db = db;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> TestMaterial(int id)
    {
        var material = await _db.Materials.FindAsync(id);
        if (material == null) return NotFound("Material not found");

        try 
        {
            var res = await _aiService.GenerateQuestionsAsync(material, 3);
            return Ok(res);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                Message = ex.Message, 
                StackTrace = ex.StackTrace,
                Inner = ex.InnerException?.Message 
            });
        }
    }
}
