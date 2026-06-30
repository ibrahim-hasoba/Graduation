using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Graduation.BLL.Errors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Graduation.BLL.Services.Implementations
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageService> _logger;
        private readonly IConfiguration _configuration;

        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly long _maxFileSize = 5 * 1024 * 1024;
        private readonly int _maxWidth;
        private readonly int _maxHeight;
        private readonly int _quality;
        private readonly int _thumbWidth;
        private readonly int _thumbHeight;

        public ImageService(
            IWebHostEnvironment environment,
            ILogger<ImageService> logger,
            IConfiguration configuration)
        {
            _environment = environment;
            _logger = logger;
            _configuration = configuration;

            var imgConfig = configuration.GetSection("ImageProcessing");
            _maxWidth = imgConfig.GetValue<int>("MaxWidth");
            if (_maxWidth <= 0) _maxWidth = 1920;
            _maxHeight = imgConfig.GetValue<int>("MaxHeight");
            if (_maxHeight <= 0) _maxHeight = 1080;
            _quality = imgConfig.GetValue<int>("Quality");
            if (_quality <= 0 || _quality > 100) _quality = 85;
            _thumbWidth = imgConfig.GetValue<int>("ThumbnailWidth");
            if (_thumbWidth <= 0) _thumbWidth = 300;
            _thumbHeight = imgConfig.GetValue<int>("ThumbnailHeight");
            if (_thumbHeight <= 0) _thumbHeight = 300;
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            if (!await ValidateImageAsync(file))
                throw new BadRequestException("Invalid image file");

            var uploadsFolder = EnsureFolder(folder);
            var fileName = $"{Guid.NewGuid()}.webp";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using var image = await Image.LoadAsync(file.OpenReadStream());
            image.Mutate(x =>
            {
                x.AutoOrient();
                if (image.Width > _maxWidth || image.Height > _maxHeight)
                    x.Resize(new ResizeOptions
                    {
                        Size = new Size(_maxWidth, _maxHeight),
                        Mode = ResizeMode.Max
                    });
            });

            await image.SaveAsWebpAsync(filePath, new WebpEncoder { Quality = _quality });

            _logger.LogInformation("Image saved: {FilePath} ({Width}x{Height}, {Size} bytes)",
                filePath, image.Width, image.Height, new FileInfo(filePath).Length);

            return $"/uploads/{folder}/{fileName}";
        }

        public async Task<List<string>> UploadImagesAsync(List<IFormFile> files, string folder)
        {
            var urls = new List<string>();
            foreach (var file in files)
                urls.Add(await UploadImageAsync(file, folder));
            return urls;
        }

        public async Task<(string imageUrl, string thumbnailUrl)> UploadProductImageAsync(IFormFile file)
        {
            if (!await ValidateImageAsync(file))
                throw new BadRequestException("Invalid image file");

            var uploadsFolder = EnsureFolder("products");
            var thumbsFolder = EnsureFolder("products/thumbnails");

            var baseName = Guid.NewGuid().ToString();
            var imagePath = Path.Combine(uploadsFolder, $"{baseName}.webp");
            var thumbPath = Path.Combine(thumbsFolder, $"{baseName}.webp");

            using var image = await Image.LoadAsync(file.OpenReadStream());
            image.Mutate(x => x.AutoOrient());

            var fullWidth = image.Width;
            var fullHeight = image.Height;

            image.Mutate(x =>
            {
                if (fullWidth > _maxWidth || fullHeight > _maxHeight)
                    x.Resize(new ResizeOptions
                    {
                        Size = new Size(_maxWidth, _maxHeight),
                        Mode = ResizeMode.Max
                    });
            });

            await image.SaveAsWebpAsync(imagePath, new WebpEncoder { Quality = _quality });

            image.Mutate(x =>
                x.Resize(new ResizeOptions
                {
                    Size = new Size(_thumbWidth, _thumbHeight),
                    Mode = ResizeMode.Max
                }));

            await image.SaveAsWebpAsync(thumbPath, new WebpEncoder { Quality = _quality });

            _logger.LogInformation("Product image saved: {Path}, thumb: {ThumbPath}",
                imagePath, thumbPath);

            return ($"/uploads/products/{baseName}.webp", $"/uploads/products/thumbnails/{baseName}.webp");
        }

        public async Task<List<(string imageUrl, string thumbnailUrl)>> UploadProductImagesAsync(List<IFormFile> files)
        {
            var results = new List<(string, string)>();
            foreach (var file in files)
                results.Add(await UploadProductImageAsync(file));
            return results;
        }

        public Task<bool> DeleteImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                    return Task.FromResult(false);

                _logger.LogInformation("Deleting image: {ImageUrl}", imageUrl);

                string fileName;
                if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://"))
                {
                    var uri = new Uri(imageUrl);
                    fileName = Path.GetFileName(uri.LocalPath);
                }
                else
                {
                    fileName = Path.GetFileName(imageUrl);
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    _logger.LogWarning("Could not extract filename from: {ImageUrl}", imageUrl);
                    return Task.FromResult(false);
                }

                var wwwrootPath = ResolveWwwRoot();
                var uploadsPath = Path.Combine(wwwrootPath, "uploads");

                if (!Directory.Exists(uploadsPath))
                    return Task.FromResult(false);

                var foundFiles = Directory.GetFiles(uploadsPath, fileName, SearchOption.AllDirectories);
                foreach (var foundFile in foundFiles)
                {
                    try
                    {
                        File.Delete(foundFile);
                        _logger.LogInformation("Deleted: {FilePath}", foundFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete: {FilePath}", foundFile);
                    }
                }

                return Task.FromResult(foundFiles.Length > 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete image: {ImageUrl}", imageUrl);
                return Task.FromResult(false);
            }
        }

        public string? GetFullImageUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            if (relativePath.StartsWith("http://") || relativePath.StartsWith("https://"))
                return relativePath;

            var baseUrl = _configuration["AppSettings:BaseUrl"]?.TrimEnd('/')
                          ?? "https://heka.runasp.net";

            if (!relativePath.StartsWith("/"))
                relativePath = "/" + relativePath;

            return $"{baseUrl}{relativePath}";
        }

        public Task<bool> ValidateImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Task.FromResult(false);

            if (file.Length > _maxFileSize)
            {
                _logger.LogWarning("Image too large: {FileSize} bytes", file.Length);
                return Task.FromResult(false);
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Invalid extension: {Extension}", extension);
                return Task.FromResult(false);
            }

            try
            {
                using var stream = file.OpenReadStream();
                var header = new byte[12];
                var read = stream.Read(header, 0, header.Length);

                if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                    return Task.FromResult(true);
                if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                    return Task.FromResult(true);
                if (read >= 6 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
                    return Task.FromResult(true);
                if (read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x46 && header[11] == 0x50)
                    return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read header for validation");
                return Task.FromResult(false);
            }

            _logger.LogWarning("Magic-bytes validation failed: {FileName}", file.FileName);
            return Task.FromResult(false);
        }

        private string EnsureFolder(string subfolder)
        {
            var wwwroot = ResolveWwwRoot();
            var path = Path.Combine(wwwroot, "uploads", subfolder);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        private string ResolveWwwRoot()
        {
            var root = _environment.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_environment.ContentRootPath, "wwwroot");
                if (!Directory.Exists(root))
                    Directory.CreateDirectory(root);
            }
            return root;
        }
    }
}
