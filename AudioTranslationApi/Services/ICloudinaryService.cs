using AudioTranslationApi.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace AudioTranslationApi.Services;

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> SaveAudioAsync(byte[] fileBuffer);
    Task DeleteAudioAsync(string publicId);
}

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly IConfiguration _configuration;

    public CloudinaryService(IConfiguration configuration)
    {
        _configuration = configuration;

        var account = new Account(
            _configuration["Cloudinary:CloudName"],
            _configuration["Cloudinary:ApiKey"],
            _configuration["Cloudinary:ApiSecret"]
        );

        _cloudinary = new Cloudinary(account);
    }

    public async Task<CloudinaryUploadResult> SaveAudioAsync(byte[] fileBuffer)
    {
        using var stream = new MemoryStream(fileBuffer);

        var uploadParams = new VideoUploadParams
        {
            File = new FileDescription("audio.wav", stream),
            Folder = "translated_audio"
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
        {
            throw new Exception($"Cloudinary upload failed: {result.Error.Message}");
        }

        return new CloudinaryUploadResult
        {
            Url = result.SecureUrl.ToString(),
            PublicId = result.PublicId
        };
    }

    public async Task DeleteAudioAsync(string publicId)
    {
        var deleteParams = new DeletionParams(publicId)
        {
            ResourceType = ResourceType.Video
        };

        await _cloudinary.DestroyAsync(deleteParams);
    }
}