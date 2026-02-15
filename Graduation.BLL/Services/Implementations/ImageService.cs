using Shared.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Graduation.BLL.Services.Implementations
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageService> _logger;
        private readonly IConfiguration _configuration;

        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly long _maxFileSize = 5 * 1024 * 1024; // 5MB

        public ImageService(
            IWebHostEnvironment environment,
            ILogger<ImageService> logger,
            IConfiguration configuration)
        {
            _environment = environment;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            // Validate image
            if (!await ValidateImageAsync(file))
                throw new BadRequestException("Invalid image file");

            // Create folder if it doesn't exist
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Generate unique filename
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Save file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Return relative URL
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "http://localhost:5069";
            var imageUrl = $"{baseUrl}/uploads/{folder}/{uniqueFileName}";

            _logger.LogInformation("Image uploaded successfully: {ImageUrl}", imageUrl);

            return imageUrl;
        }

        public async Task<List<string>> UploadImagesAsync(List<IFormFile> files, string folder)
        {
            var imageUrls = new List<string>();

            foreach (var file in files)
            {
                var imageUrl = await UploadImageAsync(file, folder);
                imageUrls.Add(imageUrl);
            }

            return imageUrls;
        }

        public Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                    return Task.FromResult(false);

                // Extract file path from URL
                var uri = new Uri(imageUrl);
                var relativePath = uri.AbsolutePath.TrimStart('/');
                var filePath = Path.Combine(_environment.WebRootPath, relativePath);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Image deleted successfully: {ImageUrl}", imageUrl);
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete image: {ImageUrl}", imageUrl);
                return Task.FromResult(false);
            }
        }

        public Task<bool> ValidateImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Task.FromResult(false);

            // Check file size
            if (file.Length > _maxFileSize)
            {
                _logger.LogWarning("Image file too large: {FileSize} bytes", file.Length);
                return Task.FromResult(false);
            }

            // Check file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Invalid image extension: {Extension}", extension);
                return Task.FromResult(false);
            }

            // Check MIME type
            var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            if (!allowedMimeTypes.Contains(file.ContentType.ToLower()))
            {
                _logger.LogWarning("Invalid image MIME type: {MimeType}", file.ContentType);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }
}