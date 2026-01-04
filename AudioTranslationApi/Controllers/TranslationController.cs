using AudioTranslationApi.Models;
using AudioTranslationApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AudioTranslationApi.Controllers;

[ApiController]
[Route("")]
public class TranslationController : ControllerBase
{
    private readonly IJobManager _jobManager;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IPythonTranslationService _pythonService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TranslationController> _logger;

    public TranslationController(
        IJobManager jobManager,
        ICloudinaryService cloudinaryService,
        IPythonTranslationService pythonService,
        IConfiguration configuration,
        ILogger<TranslationController> logger)
    {
        _jobManager = jobManager;
        _cloudinaryService = cloudinaryService;
        _pythonService = pythonService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("translate")]
    public async Task<IActionResult> Translate([FromForm] TranslationRequest request)
    {
        if (request.AudioFile == null || request.AudioFile.Length == 0)
        {
            return BadRequest(new { error = "audio_file is required" });
        }

        var jobId = Guid.NewGuid().ToString();
        var targetLanguages = request.TargetLanguages
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        string audioPath;
        string? uploadedPublicId = null;
        var environment = _configuration["Environment"];

        if (environment == "dev")
        {
            var pendingDir = _configuration["Storage:PendingDirectory"] ?? "./pending";
            Directory.CreateDirectory(pendingDir);

            var safeName = request.AudioFile.FileName.Replace(" ", "_");
            var filename = $"{jobId}_{safeName}";
            audioPath = Path.Combine(pendingDir, filename);

            using (var stream = new FileStream(audioPath, FileMode.Create))
            {
                await request.AudioFile.CopyToAsync(stream);
            }
        }
        else if (environment == "proto")
        {
            using var memoryStream = new MemoryStream();
            await request.AudioFile.CopyToAsync(memoryStream);
            var buffer = memoryStream.ToArray();

            var cloudinaryResult = await _cloudinaryService.SaveAudioAsync(buffer);
            audioPath = cloudinaryResult.Url;
            uploadedPublicId = cloudinaryResult.PublicId;
        }
        else
        {
            return StatusCode(500, new { error = "Invalid environment configuration" });
        }

        var job = new TranslationJob
        {
            JobId = jobId,
            Status = "queued",
            AudioPath = audioPath,
            SrcLanguage = request.SrcLanguage,
            TargetLanguages = targetLanguages,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PublicId = uploadedPublicId
        };

        _jobManager.AddJob(job);
        _logger.LogInformation("📥 Queued job {JobId}", jobId);

        var ttlMinutes = _configuration.GetValue<int>("Storage:ResultTtlMinutes", 3);

        return Ok(new TranslationResponse
        {
            JobId = jobId,
            Status = "queued",
            FetchUrl = $"/results/{jobId}",
            TtlSeconds = ttlMinutes * 60
        });
    }

    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages()
    {
        try
        {
            var languages = await _pythonService.GetLanguagesAsync();
            return Ok(languages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching languages");
            return StatusCode(500, new { error = "Failed to fetch languages" });
        }
    }

    [HttpGet("audio/{jobId}/{language}")]
    public IActionResult GetAudio(string jobId, string language)
    {
        var job = _jobManager.GetJob(jobId);

        if (job == null || job.Status != "completed")
        {
            return NotFound(new { error = "Job not found or not completed." });
        }

        var outputFile = job.OutputFiles
            .FirstOrDefault(f => f.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

        if (outputFile == null)
        {
            return NotFound(new { error = $"Audio for language \"{language}\" not found." });
        }

        var environment = _configuration["Environment"];

        if (environment == "dev")
        {
            if (!System.IO.File.Exists(outputFile.Path))
            {
                return NotFound(new { error = "Audio file not found." });
            }

            var fileBytes = System.IO.File.ReadAllBytes(outputFile.Path);
            return File(fileBytes, "audio/wav", Path.GetFileName(outputFile.Path));
        }
        else if (environment == "proto")
        {
            return Redirect(outputFile.Path);
        }

        return StatusCode(500, new { error = "Invalid environment configuration" });
    }

    [HttpGet("results/{jobId}")]
    public IActionResult GetJobStatus(string jobId)
    {
        var job = _jobManager.GetJob(jobId);

        if (job == null)
        {
            return NotFound(new { error = "Job ID not found." });
        }

        var ttlMinutes = _configuration.GetValue<int>("Storage:ResultTtlMinutes", 3);

        return Ok(new JobStatusResponse
        {
            JobId = job.JobId,
            Status = job.Status,
            Result = job.Result,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            TtlSeconds = ttlMinutes * 60
        });
    }

    [HttpGet("results")]
    public IActionResult GetAllCompletedJobs()
    {
        var completedJobs = _jobManager.GetCompletedJobs();
        return Ok(completedJobs);
    }
}