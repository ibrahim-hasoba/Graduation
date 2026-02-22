using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly IImageService _imageService;

        public ImagesController(IImageService imageService)
        {
            _imageService = imageService;
        }

        /// <summary>
        /// Upload a single image
        /// </summary>
        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] ImageUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new ApiResponse(400, "No file provided"));

            var imageUrl = await _imageService.UploadImageAsync(request.File, request.Folder ?? "general");

            return Ok(new
            {
                success = true,
                message = "Image uploaded successfully",
                data = new { imageUrl }
            });
        }

        /// <summary>
        /// Upload multiple images
        /// </summary>
        [HttpPost("upload-multiple")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadMultipleImages([FromForm] MultipleImagesUploadRequest request)
        {
            if (request.Files == null || !request.Files.Any())
                return BadRequest(new ApiResponse(400, "No files provided"));

            var imageUrls = await _imageService.UploadImagesAsync(request.Files, request.Folder ?? "general");

            return Ok(new
            {
                success = true,
                message = $"{imageUrls.Count} images uploaded successfully",
                data = new { imageUrls }
            });
        }

        /// <summary>
        /// Upload product images
        /// </summary>
        [HttpPost("upload-product")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProductImages([FromForm] ProductImagesUploadRequest request)
        {
            if (request.Files == null || !request.Files.Any())
                return BadRequest(new ApiResponse(400, "No files provided"));

            if (request.Files.Count > 5)
                return BadRequest(new ApiResponse(400, "Maximum 5 images allowed per product"));

            var imageUrls = await _imageService.UploadImagesAsync(request.Files, "products");

            return Ok(new
            {
                success = true,
                message = $"{imageUrls.Count} product images uploaded successfully",
                data = new { imageUrls }
            });
        }

        /// <summary>
        /// Upload vendor logo
        /// </summary>
        [HttpPost("upload-logo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadVendorLogo([FromForm] SingleFileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new ApiResponse(400, "No file provided"));

            var imageUrl = await _imageService.UploadImageAsync(request.File, "vendors/logos");

            return Ok(new
            {
                success = true,
                message = "Logo uploaded successfully",
                data = new { imageUrl }
            });
        }

        /// <summary>
        /// Upload vendor banner
        /// </summary>
        [HttpPost("upload-banner")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadVendorBanner([FromForm] SingleFileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new ApiResponse(400, "No file provided"));

            var imageUrl = await _imageService.UploadImageAsync(request.File, "vendors/banners");

            return Ok(new
            {
                success = true,
                message = "Banner uploaded successfully",
                data = new { imageUrl }
            });
        }

        /// <summary>
        /// Delete an image
        /// </summary>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteImage([FromQuery] string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return BadRequest(new ApiResponse(400, "Image URL is required"));

            var deleted = await _imageService.DeleteImageAsync(imageUrl);

            if (!deleted)
                return NotFound(new ApiResponse(404, "Image not found"));

            return Ok(new
            {
                success = true,
                message = "Image deleted successfully"
            });
        }
    }

    // DTOs for file uploads (Swagger-compatible)
    public class ImageUploadRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? Folder { get; set; }
    }

    public class SingleFileUploadRequest
    {
        public IFormFile File { get; set; } = null!;
    }

    public class MultipleImagesUploadRequest
    {
        public List<IFormFile> Files { get; set; } = new();
        public string? Folder { get; set; }
    }

    public class ProductImagesUploadRequest
    {
        public List<IFormFile> Files { get; set; } = new();
    }
}