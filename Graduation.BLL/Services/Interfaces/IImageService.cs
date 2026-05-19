using Microsoft.AspNetCore.Http;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IImageService
    {
        Task<string> UploadImageAsync(IFormFile file, string folder);
        Task<List<string>> UploadImagesAsync(List<IFormFile> files, string folder);
        Task<(string imageUrl, string thumbnailUrl)> UploadProductImageAsync(IFormFile file);
        Task<List<(string imageUrl, string thumbnailUrl)>> UploadProductImagesAsync(List<IFormFile> files);
        Task<bool> DeleteImageAsync(string imageUrl);
        Task<bool> ValidateImageAsync(IFormFile file);
        string? GetFullImageUrl(string relativePath);
    }
}
