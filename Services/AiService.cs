using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using KidsLearningPlatform.Api.DTOs.Materials;
using KidsLearningPlatform.Api.Models;
using UglyToad.PdfPig;
using OpenAI.Chat;
using OpenAI.Audio;
using System.ClientModel;

namespace KidsLearningPlatform.Api.Services;

public interface IAiService
{
    Task<List<MaterialQuestionDto>> GenerateQuestionsAsync(Material material, int count);
}

public class AiService : IAiService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AiService> _logger;

    public AiService(IConfiguration configuration, IWebHostEnvironment env, ILogger<AiService> logger)
    {
        _configuration = configuration;
        _env = env;
        _logger = logger;
    }

    public async Task<List<MaterialQuestionDto>> GenerateQuestionsAsync(Material material, int count)
    {
        count = Math.Clamp(count, 3, 10);
        string extractedText = string.Empty;

        try
        {
            var url = material.Url ?? "";
            string localPath = url;
            
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uriResult))
            {
                localPath = uriResult.LocalPath;
            }
            
            localPath = localPath.TrimStart('/', '\\');
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var filePath = Path.Combine(webRoot, localPath);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found at path {FilePath} for material ID {MaterialId}. Falling back to default generation.", filePath, material.Id);
                return GenerateMockQuestions(material, count);
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // 1. If it is PDF
            if (extension == ".pdf")
            {
                _logger.LogInformation("Extracting text from PDF material ID {MaterialId}", material.Id);
                extractedText = ExtractTextFromPdf(filePath);
            }
            // 2. If it is Video or Audio
            else if (IsMediaFile(extension))
            {
                _logger.LogInformation("Extracting audio/transcription from Media material ID {MaterialId}", material.Id);
                extractedText = await TranscribeAudioAsync(filePath);
            }
            else
            {
                _logger.LogWarning("Unsupported file type {Extension} for AI extraction. Falling back to default generation.", extension);
                return GenerateMockQuestions(material, count);
            }

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogWarning("No content could be extracted from material ID {MaterialId}. Falling back to default generation.", material.Id);
                return GenerateMockQuestions(material, count);
            }

            // 3. Generate questions from text
            return await GenerateQuestionsFromTextAsync(material.Id, material.Name ?? "Material", extractedText, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during AI Generation for Material ID {MaterialId}. Falling back to generated mock questions.", material.Id);
            return GenerateMockQuestions(material, count);
        }
    }

    private string ExtractTextFromPdf(string filePath)
    {
        try 
        {
            using var pdfDocument = PdfDocument.Open(filePath);
            var textBuilder = new System.Text.StringBuilder();

            foreach (var page in pdfDocument.GetPages())
            {
                textBuilder.AppendLine(page.Text);
            }

            return textBuilder.ToString();
        } 
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Failed to extract text from PDF file at path {FilePath}", filePath);
            throw;
        }
    }

    private async Task EnsureFfmpegInstalledAsync()
    {
        var ffMpegFolderPath = Path.Combine(_env.WebRootPath ?? Directory.GetCurrentDirectory(), "ffmpeg");
        if (!Directory.Exists(ffMpegFolderPath))
        {
            Directory.CreateDirectory(ffMpegFolderPath);
        }
        
        Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffMpegFolderPath);
        
        var exePath = Path.Combine(ffMpegFolderPath, "ffmpeg.exe");
        if (!File.Exists(exePath))
        {
            _logger.LogInformation("FFmpeg not found locally. Downloading to {Path}...", ffMpegFolderPath);
            await Xabe.FFmpeg.Downloader.FFmpegDownloader.GetLatestVersion(Xabe.FFmpeg.Downloader.FFmpegVersion.Official, ffMpegFolderPath);
            _logger.LogInformation("FFmpeg download complete.");
        }
    }

    private async Task<string> TranscribeAudioAsync(string filePath)
    {
        var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception("OpenAI API Key is missing. Cannot transcribe media.");
        }

        await EnsureFfmpegInstalledAsync();
        
        var client = new OpenAI.OpenAIClient(apiKey);
        var audioClient = client.GetAudioClient("whisper-1");
        
        var transcriptionOpts = new AudioTranscriptionOptions
        {
            ResponseFormat = AudioTranscriptionFormat.Text
        };

        // 1. Extract audio to MP3 at 64kbps to drastically reduce size.
        _logger.LogInformation("Extracting and compressing audio from {FilePath}", filePath);
        var tempMp3Path = Path.Combine(Path.GetDirectoryName(filePath)!, $"{Guid.NewGuid()}.mp3");
        
        try 
        {
            var mediaInfo = await Xabe.FFmpeg.FFmpeg.GetMediaInfo(filePath);
            var audioStream = mediaInfo.AudioStreams.FirstOrDefault();
            
            if (audioStream == null)
            {
                throw new Exception("No audio stream found in the media file.");
            }

            var conversion = Xabe.FFmpeg.FFmpeg.Conversions.New()
                .AddStream(audioStream)
                .SetAudioBitrate(64000)
                .SetOutputFormat(Xabe.FFmpeg.Format.mp3)
                .SetOutput(tempMp3Path);

            await conversion.Start();
            _logger.LogInformation("Audio extracted successfully to {TempPath}", tempMp3Path);

            var extractedFileInfo = new FileInfo(tempMp3Path);
            long fileSizeInMb = extractedFileInfo.Length / (1024 * 1024);
            
            // If it's small enough, just send it directly
            if (fileSizeInMb <= 24)
            {
                using var fileStream = File.OpenRead(tempMp3Path);
                var result = await audioClient.TranscribeAudioAsync(fileStream, Path.GetFileName(tempMp3Path), transcriptionOpts);
                return result.Value.Text;
            }

            // 2. If it is STILL too big (>24MB), we break it into chunks with 15 second overlaps.
            _logger.LogInformation("Extracted MP3 is still {Size}MB. Chunking with overlaps...", fileSizeInMb);
            
            var extractedInfo = await Xabe.FFmpeg.FFmpeg.GetMediaInfo(tempMp3Path);
            var totalDuration = extractedInfo.Duration;
            
            var fullTranscription = new System.Text.StringBuilder();
            
            TimeSpan chunkSize = TimeSpan.FromMinutes(15);
            TimeSpan overlap = TimeSpan.FromSeconds(15);
            TimeSpan currentStart = TimeSpan.Zero;
            
            int chunkIndex = 1;
            var createdChunks = new List<string>();

            while (currentStart < totalDuration)
            {
                var chunkMp3Path = Path.Combine(Path.GetDirectoryName(filePath)!, $"{Guid.NewGuid()}_chunk{chunkIndex}.mp3");
                createdChunks.Add(chunkMp3Path);
                
                var chunkEnd = currentStart + chunkSize;
                if (chunkEnd > totalDuration) chunkEnd = totalDuration;
                
                var chunkDuration = chunkEnd - currentStart;

                _logger.LogInformation("Extracting Chunk {Index}: {Start} to {End}", chunkIndex, currentStart, chunkEnd);

                var chunkConversion = Xabe.FFmpeg.FFmpeg.Conversions.New()
                    .AddParameter($"-ss {currentStart} -t {chunkDuration} -i \"{tempMp3Path}\"")
                    .SetOutputFormat(Xabe.FFmpeg.Format.mp3)
                    .SetOutput(chunkMp3Path);

                await chunkConversion.Start();

                using (var chunkStream = File.OpenRead(chunkMp3Path))
                {
                    _logger.LogInformation("Sending Chunk {Index} to Whisper API...", chunkIndex);
                    // Add delay to prevent OpenAI rate limiting on rapid chunks
                    if (chunkIndex > 1) await Task.Delay(2000); 
                    
                    var result = await audioClient.TranscribeAudioAsync(chunkStream, Path.GetFileName(chunkMp3Path), transcriptionOpts);
                    fullTranscription.AppendLine(result.Value.Text);
                }

                currentStart += (chunkSize - overlap);
                chunkIndex++;
            }

            // Clean up chunk files
            foreach(var chunk in createdChunks)
            {
                if (File.Exists(chunk)) File.Delete(chunk);
            }

            return fullTranscription.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transcribe audio. Verify FFmpeg is functioning and OpenAI limits.");
            throw;
        }
        finally
        {
            if (File.Exists(tempMp3Path))
            {
                File.Delete(tempMp3Path);
            }
        }
    }

    private async Task<List<MaterialQuestionDto>> GenerateQuestionsFromTextAsync(int materialId, string materialName, string textContent, int count)
    {
        var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception("OpenAI API Key is missing. Cannot generate questions using LLM.");
        }

        var client = new OpenAI.OpenAIClient(apiKey);
        var chatClient = client.GetChatClient("gpt-4o-mini");

        // Limit the text to avoid exceeding token limits (e.g., take the first 10,000 chars roughly)
        if (textContent.Length > 20000)
            textContent = textContent.Substring(0, 20000) + "...(truncated)";

        string systemPrompt = $@"You are an educational assistant. 
1. Analyze the following text extracted from a material titled '{materialName}'.
2. Based only on the provided text, generate exactly {count} multiple-choice questions.
3. Return ONLY a valid JSON array of objects. Do not wrap it in markdown. 
Each object must have the following properties:
- questionText: string
- optA: string
- optB: string
- optC: string
- optD: string
- correctAnswer: string (must perfectly match one of the optA/optB/optC/optD strings)";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(textContent)
        };

        try 
        {
            _logger.LogInformation("Sending text to GPT-4o-mini to generate questions...");
            var response = await chatClient.CompleteChatAsync(messages);
            var rawJson = response.Value.Content[0].Text;
            _logger.LogInformation("Successfully received JSON response from GPT-4o-mini.");

            // Clean up markdown serialization if present
            rawJson = rawJson.Trim();
            if (rawJson.StartsWith("```json"))
                rawJson = rawJson.Substring("```json".Length);
            if (rawJson.StartsWith("```"))
                rawJson = rawJson.Substring(3);
            if (rawJson.EndsWith("```"))
                rawJson = rawJson.Substring(0, rawJson.Length - 3);
                
            rawJson = rawJson.Trim();

            var aiQuestions = JsonSerializer.Deserialize<List<AiParsedQuestion>>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (aiQuestions == null || !aiQuestions.Any())
                throw new Exception("Failed to deserialize questions from LLM response.");

            var dtos = new List<MaterialQuestionDto>();
            for (int i = 0; i < aiQuestions.Count; i++)
            {
                var q = aiQuestions[i];
                var options = new List<string> { q.OptA, q.OptB, q.OptC, q.OptD };
                
                dtos.Add(new MaterialQuestionDto
                {
                    MaterialId = materialId,
                    QuestionText = q.QuestionText ?? "Generated Question",
                    OptionsJson = JsonSerializer.Serialize(options),
                    CorrectAnswer = q.CorrectAnswer ?? q.OptA,
                    OrderIndex = i
                });
            }
            
            return dtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate questions from text. LLM connection or parsing failed.");
            throw;
        }
    }

    private class AiParsedQuestion
    {
        public string? QuestionText { get; set; }
        public string? OptA { get; set; }
        public string? OptB { get; set; }
        public string? OptC { get; set; }
        public string? OptD { get; set; }
        public string? CorrectAnswer { get; set; }
    }

    private bool IsMediaFile(string extension)
    {
        var mediaExtensions = new[] { ".mp3", ".mp4", ".mpeg", ".mpga", ".m4a", ".wav", ".webm" };
        return mediaExtensions.Contains(extension);
    }

    private List<MaterialQuestionDto> GenerateMockQuestions(Material material, int count)
    {
        var generatedQuestions = new List<MaterialQuestionDto>();
        string contextSubject = string.IsNullOrWhiteSpace(material.Name) ? "General Knowledge" : material.Name;

        for (int i = 0; i < count; i++)
        {
            var options = GenerateMockOptions(contextSubject, i, out string correctAnswer);
            
            generatedQuestions.Add(new MaterialQuestionDto
            {
                MaterialId = material.Id,
                QuestionText = $"Based on {contextSubject}, what is the significance of concept #{i + 1}?",
                OptionsJson = JsonSerializer.Serialize(options),
                CorrectAnswer = correctAnswer,
                OrderIndex = i
            });
        }

        return generatedQuestions;
    }

    private List<string> GenerateMockOptions(string subject, int index, out string correct)
    {
        var options = new List<string>
        {
            $"{subject} Concept {index} Option A",
            $"{subject} Concept {index} Option B (Correct)",
            $"{subject} Concept {index} Option C",
            $"{subject} Concept {index} Option D"
        };
        
        // Shuffle options
        var random = new Random();
        int n = options.Count;  
        while (n > 1) {  
            n--;  
            int k = random.Next(n + 1);  
            string value = options[k];  
            options[k] = options[n];  
            options[n] = value;  
        }  

        correct = options.First(o => o.Contains("(Correct)"));
        return options;
    }
}
