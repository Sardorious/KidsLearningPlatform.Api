using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using KidsLearningPlatform.Api.DTOs.Materials;
using KidsLearningPlatform.Api.Models;
using UglyToad.PdfPig;
using OpenAI.Chat;
using OpenAI.Audio;
using System.ClientModel;
using Microsoft.Extensions.Caching.Memory;

namespace KidsLearningPlatform.Api.Services;

public record TutorChatResponse(string Reply, string Subject);
public record WritingCheckResponse(int GrammarScore, int VocabularyScore, int ClarityScore, string ToneAnalysis, string Feedback, string CorrectedText);
public record SpeakingCheckResponse(string Transcription, int FluencyScore, int GrammarScore, int PronunciationScore, string Feedback);
public record LessonPlanResponse(string Topic, int AgeGroup, string Objectives, string WarmUp, string MainActivity, string Assessment, string Homework, string TeacherNotes);
public record ProgressReportResponse(string Summary, string Strengths, string AreasToImprove, string Recommendations);

public interface IAiService
{
    Task<List<MaterialQuestionDto>> GenerateQuestionsAsync(Material material, int count);
    IAsyncEnumerable<string> ChatWithTutorStreamAsync(string subject, string message, string grade);
    Task<WritingCheckResponse> CheckWritingAsync(string? text, string grade, IFormFile? imageFile = null);
    Task<SpeakingCheckResponse> CheckSpeakingAsync(IFormFile audioFile);
    Task<LessonPlanResponse> GenerateLessonPlanAsync(string topic, int ageGroup, string level);
    Task<ProgressReportResponse> GenerateProgressReportAsync(string studentName, int completedLessons, int xp, int coins, string recentActivity);
}

public class AiService : IAiService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AiService> _logger;
    private readonly IMemoryCache _cache;

    public AiService(IConfiguration configuration, IWebHostEnvironment env, ILogger<AiService> logger, IMemoryCache cache)
    {
        _configuration = configuration;
        _env = env;
        _logger = logger;
        _cache = cache;
    }

    private async Task<ClientResult<ChatCompletion>> CallOpenAiWithRetryAsync(ChatClient client, IEnumerable<ChatMessage> messages, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await client.CompleteChatAsync(messages);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI API call failed on attempt {Attempt} of {MaxRetries}", i + 1, maxRetries);
                if (i == maxRetries - 1) throw;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i))); // Exponential backoff: 1s, 2s, 4s...
            }
        }
        throw new InvalidOperationException("Failed to call OpenAI API after retries.");
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

    // ─── NEW AI METHODS ───────────────────────────────────────────────────────

    public async IAsyncEnumerable<string> ChatWithTutorStreamAsync(string subject, string message, string grade)
    {
        var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            yield return "I'm sorry, I'm not available right now. Please ask your teacher for help! 😊";
            yield break;
        }

        ChatClient chatClient = null!;
        string? initError = null;
        try
        {
            var client = new OpenAI.OpenAIClient(apiKey);
            chatClient = client.GetChatClient("gpt-4o-mini");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize OpenAI client");
            initError = "Oops! I had a little glitch 🔧. Please try again in a moment!";
        }

        if (initError != null)
        {
            yield return initError;
            yield break;
        }

        string systemPrompt = $@"You are Zappy 🤖, a friendly and encouraging AI tutor for kids.
You are helping a student in {grade ?? "elementary school"} with {subject ?? "their studies"}.

Rules:
- Use simple, fun, age-appropriate language for a {grade ?? "young"} student
- Keep answers SHORT (max 4 sentences) and easy to understand
- Use emojis occasionally to make it fun
- If the student seems confused, break it down step by step
- Never give the full answer to a homework problem — give hints instead
- Always be encouraging: use phrases like 'Great question!', 'You're doing amazing!', 'Let's figure this out together!'
- If asked about something inappropriate or off-topic, kindly redirect back to studying";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(message)
        };

        AsyncCollectionResult<StreamingChatCompletionUpdate> responseStream = null!;
        string? streamError = null;
        try
        {
            responseStream = chatClient.CompleteChatStreamingAsync(messages);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "AI Tutor chat failed to start streaming");
             streamError = "Oops! I couldn't connect to my brain. Please try again!";
        }

        if (streamError != null || responseStream == null)
        {
            yield return streamError ?? "Oops! I couldn't connect to my brain. Please try again!";
            yield break;
        }

        await foreach (var update in responseStream)
        {
            foreach (var contentPart in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    yield return contentPart.Text;
                }
            }
        }
    }


    public async Task<WritingCheckResponse> CheckWritingAsync(string? text, string grade, IFormFile? imageFile = null)
    {
        var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return new WritingCheckResponse(0, 0, 0, "N/A", "AI is not configured. Please set the OpenAI API key.", text);

        try
        {
            var client = new OpenAI.OpenAIClient(apiKey);
            var chatClient = client.GetChatClient("gpt-4o-mini");

            string systemPrompt = $@"You are an expert writing coach evaluating student writing for a {grade ?? "primary school"} level student.
Analyze the provided text and return ONLY a valid JSON object with these fields:
{{
  ""grammarScore"": <integer 0-100>,
  ""vocabularyScore"": <integer 0-100>,
  ""clarityScore"": <integer 0-100>,
  ""toneAnalysis"": ""<one sentence describing the tone and style>"",
  ""feedback"": ""<2-4 actionable sentences of constructive feedback appropriate for the grade level>"",
  ""correctedText"": ""<the original text with corrections applied, keeping the student's voice>""
}}
Do NOT wrap in markdown. Return ONLY the JSON.";

            // Build the multi-part user message
            var contentParts = new List<ChatMessageContentPart>();
            if (!string.IsNullOrWhiteSpace(text))
            {
                contentParts.Add(ChatMessageContentPart.CreateTextPart(text));
            }
            if (imageFile != null && imageFile.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await imageFile.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();
                var imageBase64 = Convert.ToBase64String(imageBytes);
                var mimeType = imageFile.ContentType ?? "image/jpeg";
                var dataUri = new Uri($"data:{mimeType};base64,{imageBase64}");
                contentParts.Add(ChatMessageContentPart.CreateImagePart(dataUri, ChatImageDetailLevel.High));
                
                if (string.IsNullOrWhiteSpace(text))
                {
                   contentParts.Add(ChatMessageContentPart.CreateTextPart("Please evaluate the handwriting in this attached image."));
                }
            }

            if (contentParts.Count == 0)
            {
                 return new WritingCheckResponse(0, 0, 0, "Error", "No text or image provided to analyze.", text ?? "");
            }

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(contentParts)
            };

            var response = await CallOpenAiWithRetryAsync(chatClient, messages);
            var rawJson = response.Value.Content[0].Text?.Trim() ?? "{}";
            rawJson = rawJson.TrimStart('`').TrimEnd('`');
            if (rawJson.StartsWith("json")) rawJson = rawJson.Substring(4);

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            return new WritingCheckResponse(
                root.TryGetProperty("grammarScore", out var g) ? g.GetInt32() : 70,
                root.TryGetProperty("vocabularyScore", out var v) ? v.GetInt32() : 70,
                root.TryGetProperty("clarityScore", out var c) ? c.GetInt32() : 70,
                root.TryGetProperty("toneAnalysis", out var t) ? t.GetString() ?? "" : "",
                root.TryGetProperty("feedback", out var f) ? f.GetString() ?? "" : "",
                root.TryGetProperty("correctedText", out var ct) ? ct.GetString() ?? text ?? "" : text ?? ""
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Writing check failed");
            return new WritingCheckResponse(0, 0, 0, "Error", "Could not analyze writing at this time. Please try again.", text ?? "");
        }
    }

    public async Task<SpeakingCheckResponse> CheckSpeakingAsync(IFormFile audioFile)
    {
        var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return new SpeakingCheckResponse("AI not configured.", 0, 0, 0, "Please set the OpenAI API key.");

        try
        {
            var client = new OpenAI.OpenAIClient(apiKey);
            var audioClient = client.GetAudioClient("whisper-1");
            var chatClient = client.GetChatClient("gpt-4o-mini");

            // Step 1: Transcribe 
            using var stream = audioFile.OpenReadStream();
            var transcriptionOpts = new AudioTranscriptionOptions { ResponseFormat = AudioTranscriptionFormat.Text };
            var transcriptionResult = await audioClient.TranscribeAudioAsync(stream, audioFile.FileName, transcriptionOpts);
            var transcription = transcriptionResult.Value.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(transcription))
                return new SpeakingCheckResponse("", 0, 0, 0, "Could not detect speech in the audio. Please record again in a quiet environment.");

            // Step 2: Evaluate with GPT
            string systemPrompt = @"You are an expert speaking and pronunciation coach for language learners.
Given a transcription of a student's spoken English, evaluate it and return ONLY a valid JSON object:
{
  ""fluencyScore"": <integer 0-100 measuring smoothness and natural flow>,
  ""grammarScore"": <integer 0-100 measuring grammatical correctness>,
  ""pronunciationScore"": <integer 0-100 estimated from text clarity and word choices>,
  ""feedback"": ""<2-4 specific, encouraging sentences about what was good and what to improve>""
}
Do NOT wrap in markdown. Return ONLY the JSON.";

            var evalMessages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage($"Student transcription: \"{transcription}\"")
            };

            var evalResponse = await CallOpenAiWithRetryAsync(chatClient, evalMessages);
            var rawJson = evalResponse.Value.Content[0].Text?.Trim() ?? "{}";
            rawJson = rawJson.TrimStart('`').TrimEnd('`');
            if (rawJson.StartsWith("json")) rawJson = rawJson.Substring(4);

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            return new SpeakingCheckResponse(
                transcription,
                root.TryGetProperty("fluencyScore", out var fl) ? fl.GetInt32() : 70,
                root.TryGetProperty("grammarScore", out var gr) ? gr.GetInt32() : 70,
                root.TryGetProperty("pronunciationScore", out var pr) ? pr.GetInt32() : 70,
                root.TryGetProperty("feedback", out var fb) ? fb.GetString() ?? "" : ""
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speaking check failed");
            return new SpeakingCheckResponse("", 0, 0, 0, "Could not evaluate speaking at this time.");
        }
    }

    public async Task<LessonPlanResponse> GenerateLessonPlanAsync(string topic, int ageGroup, string level)
    {
        string cacheKey = $"LessonPlan_{topic}_{ageGroup}_{level}";
        if (_cache.TryGetValue(cacheKey, out LessonPlanResponse? cachedPlan) && cachedPlan != null)
        {
            _logger.LogInformation("Returning cached lesson plan for topic {Topic}", topic);
            return cachedPlan;
        }

        var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return new LessonPlanResponse(topic, ageGroup, "AI not configured.", "", "", "", "", "");

        try
        {
            var client = new OpenAI.OpenAIClient(apiKey);
            var chatClient = client.GetChatClient("gpt-4o-mini");

            string systemPrompt = $@"You are an expert curriculum designer creating lesson plans for kids aged {ageGroup}.
Create a complete lesson plan for the topic: '{topic}' at {level ?? "Beginner"} level.
Return ONLY a valid JSON object:
{{
  ""objectives"": ""<2-3 clear learning objectives>"",
  ""warmUp"": ""<5 min warm-up activity description>"",
  ""mainActivity"": ""<15-20 min main lesson description with step-by-step instructions>"",
  ""assessment"": ""<5 min assessment/check description>"",
  ""homework"": ""<optional short homework idea>"",
  ""teacherNotes"": ""<tips for the teacher, including differentiation ideas>""
}}
Do NOT wrap in markdown. Return ONLY the JSON.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage($"Topic: {topic}, Age: {ageGroup}, Level: {level}")
            };

            var response = await CallOpenAiWithRetryAsync(chatClient, messages);
            var rawJson = response.Value.Content[0].Text?.Trim() ?? "{}";
            rawJson = rawJson.TrimStart('`').TrimEnd('`');
            if (rawJson.StartsWith("json")) rawJson = rawJson.Substring(4);

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var plan = new LessonPlanResponse(
                topic, ageGroup,
                root.TryGetProperty("objectives", out var obj) ? obj.GetString() ?? "" : "",
                root.TryGetProperty("warmUp", out var wu) ? wu.GetString() ?? "" : "",
                root.TryGetProperty("mainActivity", out var ma) ? ma.GetString() ?? "" : "",
                root.TryGetProperty("assessment", out var ass) ? ass.GetString() ?? "" : "",
                root.TryGetProperty("homework", out var hw) ? hw.GetString() ?? "" : "",
                root.TryGetProperty("teacherNotes", out var tn) ? tn.GetString() ?? "" : ""
            );

            _cache.Set(cacheKey, plan, TimeSpan.FromHours(1)); // Cache for 1 hour
            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lesson plan generation failed");
            return new LessonPlanResponse(topic, ageGroup, "Could not generate lesson plan.", "", "", "", "", "");
        }
    }

    public async Task<ProgressReportResponse> GenerateProgressReportAsync(string studentName, int completedLessons, int xp, int coins, string recentActivity)
    {
        string cacheKey = $"ProgressReport_{studentName}_{completedLessons}_{xp}_{coins}_{recentActivity}";
        if (_cache.TryGetValue(cacheKey, out ProgressReportResponse? cachedReport) && cachedReport != null)
        {
            _logger.LogInformation("Returning cached progress report for {Student}", studentName);
            return cachedReport;
        }

        var apiKey = _configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return new ProgressReportResponse("AI not configured.", "", "", "");

        try
        {
            var client = new OpenAI.OpenAIClient(apiKey);
            var chatClient = client.GetChatClient("gpt-4o-mini");

            string systemPrompt = @"You are a warm, encouraging education counselor writing progress reports for parents.
Given a student's activity data, write a concise, positive progress report.
Return ONLY a valid JSON object:
{
  ""summary"": ""<2-3 sentence overall summary of the student's progress>"",
  ""strengths"": ""<1-2 specific strengths observed>"",
  ""areasToImprove"": ""<1-2 gentle suggestions for improvement>"",
  ""recommendations"": ""<2-3 specific actionable recommendations for parents to support learning at home>""
}
Do NOT wrap in markdown. Return ONLY the JSON.";

            var dataPrompt = $"Student: {studentName}. Completed Lessons: {completedLessons}. XP earned: {xp}. Coins: {coins}. Recent activity: {recentActivity}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(dataPrompt)
            };

            var response = await CallOpenAiWithRetryAsync(chatClient, messages);
            var rawJson = response.Value.Content[0].Text?.Trim() ?? "{}";
            rawJson = rawJson.TrimStart('`').TrimEnd('`');
            if (rawJson.StartsWith("json")) rawJson = rawJson.Substring(4);

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var report = new ProgressReportResponse(
                root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                root.TryGetProperty("strengths", out var st) ? st.GetString() ?? "" : "",
                root.TryGetProperty("areasToImprove", out var ai) ? ai.GetString() ?? "" : "",
                root.TryGetProperty("recommendations", out var r) ? r.GetString() ?? "" : ""
            );

            _cache.Set(cacheKey, report, TimeSpan.FromMinutes(30)); // Cache report data for 30 minutes
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Progress report generation failed");
            return new ProgressReportResponse("Could not generate report at this time.", "", "", "");
        }
    }
}
