using AudioTranslationApi.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AudioTranslationApi.Services;

public interface IPythonTranslationService
{
    Task<PythonTranslationResponse> TranslateAsync(Stream audioStream, string srcLanguage, List<string> targetLanguages);
    Task<JsonDocument> GetLanguagesAsync();
}

public class PythonTranslationService : IPythonTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PythonTranslationService> _logger;

    public PythonTranslationService(
        IConfiguration configuration,
        ILogger<PythonTranslationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
    }

    public async Task<PythonTranslationResponse> TranslateAsync(
        Stream audioStream,
        string srcLanguage,
        List<string> targetLanguages)
    {
        var url = _configuration["PythonService:TranslateUrl"]
            ?? throw new InvalidOperationException("TranslateUrl not configured");

        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(audioStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(streamContent, "audio_file", "audio.wav");
        content.Add(new StringContent(srcLanguage), "src_language");
        content.Add(new StringContent(string.Join(",", targetLanguages)), "target_languages");

        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<PythonTranslationResponse>(jsonResponse, options)
            ?? throw new Exception("Failed to deserialize response");
    }

    public async Task<JsonDocument> GetLanguagesAsync()
    {
        var url = _configuration["PythonService:LanguagesUrl"]
            ?? throw new InvalidOperationException("LanguagesUrl not configured");

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(jsonResponse);
    }
}