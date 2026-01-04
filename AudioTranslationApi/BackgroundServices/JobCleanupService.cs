using AudioTranslationApi.Services;

namespace AudioTranslationApi.BackgroundServices;

public class JobCleanupService : BackgroundService
{
    private readonly IJobManager _jobManager;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobCleanupService> _logger;

    public JobCleanupService(
        IJobManager jobManager,
        ICloudinaryService cloudinaryService,
        IConfiguration configuration,
        ILogger<JobCleanupService> logger)
    {
        _jobManager = jobManager;
        _cloudinaryService = cloudinaryService;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            await CleanupExpiredJobsAsync();
        }
    }

    private async Task CleanupExpiredJobsAsync()
    {
        var ttlMinutes = _configuration.GetValue<int>("Storage:ResultTtlMinutes", 3);
        var ttl = TimeSpan.FromMinutes(ttlMinutes);
        var expiredJobs = _jobManager.GetExpiredJobs(ttl);

        foreach (var job in expiredJobs)
        {
            var environment = _configuration["Environment"];

            foreach (var file in job.OutputFiles)
            {
                if (environment == "dev" && File.Exists(file.Path))
                {
                    File.Delete(file.Path);
                    _logger.LogInformation("🗑️ Deleted result file: {Path}", file.Path);
                }
                else if (environment == "proto" && !string.IsNullOrEmpty(file.PublicId))
                {
                    await _cloudinaryService.DeleteAudioAsync(file.PublicId);
                    _logger.LogInformation("🗑️ Deleted Cloudinary file: {PublicId}", file.PublicId);
                }
            }

            _jobManager.RemoveJob(job.JobId);
            _logger.LogInformation("🧹 Removed old job from memory: {JobId}", job.JobId);
        }
    }
}