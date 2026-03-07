using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Errors;
using System.IO;

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
            if (!await ValidateImageAsync(file))
                throw new BadRequestException("Invalid image file");

            string webRootPath = _environment.WebRootPath;

            if (string.IsNullOrEmpty(webRootPath))
            {
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
                if (!Directory.Exists(webRootPath))
                {
                    Directory.CreateDirectory(webRootPath);
                    _logger.LogInformation("Created wwwroot folder at: {WebRootPath}", webRootPath);
                }
            }

            var uploadsFolder = Path.Combine(webRootPath, "uploads", folder);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
                _logger.LogInformation("Created folder at: {UploadsFolder}", uploadsFolder);
            }

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File saved at: {FilePath}", filePath);

            var imageUrl = $"/uploads/{folder}/{uniqueFileName}";

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

                _logger.LogInformation("Attempting to delete image: {ImageUrl}", imageUrl);

                // Extract filename from the URL/path
                string fileName;

                if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://"))
                {
                    // It's a full URL - extract filename
                    var uri = new Uri(imageUrl);
                    fileName = Path.GetFileName(uri.LocalPath);
                }
                else
                {
                    // It's a relative path - extract filename
                    fileName = Path.GetFileName(imageUrl);
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    _logger.LogWarning("Could not extract filename from: {ImageUrl}", imageUrl);
                    return Task.FromResult(false);
                }

                _logger.LogInformation("Looking for file with name: {FileName}", fileName);

                // Determine the wwwroot path
                string wwwrootPath = _environment.WebRootPath;
                if (string.IsNullOrEmpty(wwwrootPath))
                {
                    wwwrootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
                }

                var uploadsPath = Path.Combine(wwwrootPath, "uploads");

                if (!Directory.Exists(uploadsPath))
                {
                    _logger.LogWarning("Uploads directory not found: {UploadsPath}", uploadsPath);
                    return Task.FromResult(false);
                }

                // Search for the file in all subdirectories of the uploads folder
                var foundFiles = Directory.GetFiles(uploadsPath, fileName, SearchOption.AllDirectories);

                foreach (var foundFile in foundFiles)
                {
                    try
                    {
                        File.Delete(foundFile);
                        _logger.LogInformation("Image deleted successfully: {FilePath}", foundFile);
                        return Task.FromResult(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete file: {FilePath}", foundFile);
                    }
                }

                _logger.LogWarning("Image file not found: {FileName} in {UploadsPath}", fileName, uploadsPath);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete image: {ImageUrl}", imageUrl);
                return Task.FromResult(false);
            }
        }

        public string GetFullImageUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            // If it's already a full URL, return as is
            if (relativePath.StartsWith("http://") || relativePath.StartsWith("https://"))
                return relativePath;

            // Get base URL from app settings
            var baseUrl = _configuration["AppSettings:BaseUrl"]?.TrimEnd('/');

            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = "https://heka.runasp.net"; // Your actual domain
            }

            // Ensure relative path starts with /
            if (!relativePath.StartsWith("/"))
                relativePath = "/" + relativePath;

            return $"{baseUrl}{relativePath}";
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

            // Check magic bytes
            try
            {
                using var stream = file.OpenReadStream();
                var header = new byte[12];
                var read = stream.Read(header, 0, header.Length);

                // JPEG (FF D8 FF)
                if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                    return Task.FromResult(true);

                // PNG (89 50 4E 47 0D 0A 1A 0A)
                if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                    return Task.FromResult(true);

                // GIF (47 49 46 38 37|39 61)
                if (read >= 6 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
                    return Task.FromResult(true);

                // WEBP (RIFF....WEBP)
                if (read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x46 && header[11] == 0x50)
                    return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read image header for validation");
                return Task.FromResult(false);
            }

            _logger.LogWarning("Image failed magic-bytes validation: {FileName}", file.FileName);
            return Task.FromResult(false);
        }
    }
}