namespace AudioTranslationApi.Models;

public class TranslationJob
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public string AudioPath { get; set; } = string.Empty;
    public string SrcLanguage { get; set; } = "English";
    public List<string> TargetLanguages { get; set; } = new();
    public long CreatedAt { get; set; }
    public long? CompletedAt { get; set; }
    public string? PublicId { get; set; }
    public TranslationResult? Result { get; set; }
    public List<OutputFile> OutputFiles { get; set; } = new();
    public string? Error { get; set; }
    public int Retries { get; set; }
}

public class TranslationResult
{
    public int SuccessfulTranslations { get; set; }
    public int TotalRequested { get; set; }
    public List<TranslationOutput> Translations { get; set; } = new();
}

public class TranslationOutput
{
    public string Language { get; set; } = string.Empty;
    public string Transcript { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string? CloudUrl { get; set; }
}

public class OutputFile
{
    public string Language { get; set; } = string.Empty;
    public string Transcript { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? CloudUrl { get; set; }
    public string? PublicId { get; set; }
}

public class PythonTranslationResponse
{
    public List<PythonTranslation> Translations { get; set; } = new();
}

public class PythonTranslation
{
    public string Language { get; set; } = string.Empty;
    public string Transcript { get; set; } = string.Empty;
    public string Audio_Base64 { get; set; } = string.Empty;
}

public class TranslationRequest
{
    public IFormFile AudioFile { get; set; } = null!;
    public string SrcLanguage { get; set; } = "English";
    public string TargetLanguages { get; set; } = string.Empty;
}

public class TranslationResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FetchUrl { get; set; } = string.Empty;
    public int TtlSeconds { get; set; }
}

public class JobStatusResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public TranslationResult? Result { get; set; }
    public long CreatedAt { get; set; }
    public long? CompletedAt { get; set; }
    public int TtlSeconds { get; set; }
}

public class CloudinaryUploadResult
{
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
}