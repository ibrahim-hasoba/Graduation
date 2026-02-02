using Microsoft.AspNetCore.Http;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IImageService
    {
        Task<string> UploadImageAsync(IFormFile file, string folder);
        Task<List<string>> UploadImagesAsync(List<IFormFile> files, string folder);
        Task<bool> DeleteImageAsync(string imageUrl);
        Task<bool> ValidateImageAsync(IFormFile file);
    }
}