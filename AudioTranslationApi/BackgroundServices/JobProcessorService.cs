using AudioTranslationApi.Models;
using AudioTranslationApi.Services;

namespace AudioTranslationApi.BackgroundServices;

public class JobProcessorService : BackgroundService
{
    private readonly IJobManager _jobManager;
    private readonly IPythonTranslationService _pythonService;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobProcessorService> _logger;
    private bool _isProcessing;

    public JobProcessorService(
        IJobManager jobManager,
        IPythonTranslationService pythonService,
        ICloudinaryService cloudinaryService,
        IConfiguration configuration,
        ILogger<JobProcessorService> logger)
    {
        _jobManager = jobManager;
        _pythonService = pythonService;
        _cloudinaryService = cloudinaryService;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessNextJobAsync();
            await Task.Delay(100, stoppingToken); // Check queue every 100ms
        }
    }

    private async Task ProcessNextJobAsync()
    {
        if (_isProcessing) return;

        var job = _jobManager.DequeueJob();
        if (job == null) return;

        _isProcessing = true;
        _logger.LogInformation("🚀 Processing job {JobId}", job.JobId);

        try
        {
            await ProcessJobAsync(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing job {JobId}", job.JobId);
            await HandleJobFailureAsync(job, ex);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ProcessJobAsync(TranslationJob job)
    {
        var environment = _configuration["Environment"];
        Stream audioStream;

        if (environment == "dev")
        {
            audioStream = File.OpenRead(job.AudioPath);
        }
        else if (environment == "proto")
        {
            using var httpClient = new HttpClient();
            var audioData = await httpClient.GetByteArrayAsync(job.AudioPath);
            audioStream = new MemoryStream(audioData);
        }
        else
        {
            throw new InvalidOperationException($"Unknown environment: {environment}");
        }

        try
        {
            var response = await _pythonService.TranslateAsync(
                audioStream,
                job.SrcLanguage,
                job.TargetLanguages);

            var outputFiles = await SaveTranslatedAudioAsync(job.JobId, response.Translations);

            job.Status = "completed";
            job.OutputFiles = outputFiles;
            job.Result = new TranslationResult
            {
                SuccessfulTranslations = outputFiles.Count,
                TotalRequested = job.TargetLanguages.Count,
                Translations = outputFiles.Select(f => new TranslationOutput
                {
                    Language = f.Language,
                    Transcript = f.Transcript,
                    DownloadUrl = $"/audio/{job.JobId}/{f.Language}",
                    CloudUrl = f.CloudUrl
                }).ToList()
            };
            job.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            await CleanupPendingFileAsync(job);

            _logger.LogInformation("✅ Job {JobId} completed", job.JobId);
        }
        finally
        {
            audioStream?.Dispose();
        }
    }

    private async Task<List<OutputFile>> SaveTranslatedAudioAsync(
        string jobId,
        List<PythonTranslation> translations)
    {
        var environment = _configuration["Environment"];
        var resultDir = _configuration["Storage:ResultDirectory"] ?? "./results";
        var outputFiles = new List<OutputFile>();

        Directory.CreateDirectory(resultDir);

        foreach (var translation in translations)
        {
            var audioBuffer = Convert.FromBase64String(translation.Audio_Base64);
            var safeLangName = translation.Language.Replace(" ", "_").Replace("/", "_").Replace(",", "_").Replace(".", "_");
            var filename = $"{jobId}_{safeLangName}.wav";

            OutputFile outputFile;

            if (environment == "dev")
            {
                var outputPath = Path.Combine(resultDir, filename);
                await File.WriteAllBytesAsync(outputPath, audioBuffer);
                _logger.LogInformation("💾 Saved translated audio for {Language} to: {Path}",
                    translation.Language, outputPath);

                outputFile = new OutputFile
                {
                    Language = translation.Language,
                    Transcript = translation.Transcript,
                    Path = outputPath,
                    CloudUrl = $"/audio/{jobId}/{translation.Language}"
                };
            }
            else if (environment == "proto")
            {
                var cloudinaryResult = await _cloudinaryService.SaveAudioAsync(audioBuffer);
                _logger.LogInformation("☁️ Uploaded translated audio for {Language} to Cloudinary: {Url}",
                    translation.Language, cloudinaryResult.Url);

                outputFile = new OutputFile
                {
                    Language = translation.Language,
                    Transcript = translation.Transcript,
                    Path = cloudinaryResult.Url,
                    CloudUrl = cloudinaryResult.Url,
                    PublicId = cloudinaryResult.PublicId
                };
            }
            else
            {
                throw new InvalidOperationException($"Unknown environment: {environment}");
            }

            outputFiles.Add(outputFile);
        }

        return outputFiles;
    }

    private async Task CleanupPendingFileAsync(TranslationJob job)
    {
        var environment = _configuration["Environment"];

        if (environment == "dev" && File.Exists(job.AudioPath))
        {
            File.Delete(job.AudioPath);
            _logger.LogInformation("🗑️ Deleted pending file: {Path}", job.AudioPath);
        }
        else if (environment == "proto" && !string.IsNullOrEmpty(job.PublicId))
        {
            await _cloudinaryService.DeleteAudioAsync(job.PublicId);
            _logger.LogInformation("🗑️ Deleted Cloudinary file: {PublicId}", job.PublicId);
        }
    }

    private async Task HandleJobFailureAsync(TranslationJob job, Exception ex)
    {
        if (job.Retries < 2)
        {
            job.Retries++;
            _logger.LogInformation("🔁 Retrying job {JobId} (attempt {Retries})",
                job.JobId, job.Retries);
            _jobManager.AddJob(job);
        }
        else
        {
            job.Status = "failed";
            job.Error = ex.Message;
            await CleanupPendingFileAsync(job);
        }
    }
}