using AudioTranslationApi.Models;

namespace AudioTranslationApi.Services;

public interface IJobManager
{
    void AddJob(TranslationJob job);
    TranslationJob? GetJob(string jobId);
    TranslationJob? DequeueJob();
    List<TranslationJob> GetCompletedJobs();
    void RemoveJob(string jobId);
    List<TranslationJob> GetExpiredJobs(TimeSpan ttl);
}

public class JobManager : IJobManager
{
    private readonly Dictionary<string, TranslationJob> _jobs = new();
    private readonly Queue<TranslationJob> _jobQueue = new();
    private readonly object _lock = new();

    public void AddJob(TranslationJob job)
    {
        lock (_lock)
        {
            _jobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
        }
    }

    public TranslationJob? GetJob(string jobId)
    {
        lock (_lock)
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }
    }

    public TranslationJob? DequeueJob()
    {
        lock (_lock)
        {
            return _jobQueue.Count > 0 ? _jobQueue.Dequeue() : null;
        }
    }

    public List<TranslationJob> GetCompletedJobs()
    {
        lock (_lock)
        {
            return _jobs.Values.Where(j => j.Status == "completed").ToList();
        }
    }

    public void RemoveJob(string jobId)
    {
        lock (_lock)
        {
            _jobs.Remove(jobId);
        }
    }

    public List<TranslationJob> GetExpiredJobs(TimeSpan ttl)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return _jobs.Values
                .Where(j => j.Status == "completed" &&
                           j.CompletedAt.HasValue &&
                           now - j.CompletedAt.Value > ttl.TotalMilliseconds)
                .ToList();
        }
    }
}