using Shared.Errors;
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
        private readonly ILanguageService _lang;

        public ImagesController(IImageService imageService, ILanguageService lang)
        {
            _imageService = imageService;
            _lang = lang;
        }

        /// <summary>Uploads a single image to a specified folder. Returns the image URL.</summary>
        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImage([FromForm] ImageUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new ApiResponse(400, _lang.GetMessage("Image_NoFile")));

            var imageUrl = await _imageService.UploadImageAsync(request.File, request.Folder ?? "general");

            return Ok(new
            {
                success = true,
                message = _lang.GetMessage("Image_Uploaded"),
                data = new { imageUrl }
            });
        }

        /// <summary>Uploads multiple images to a specified folder. Returns an array of image URLs.</summary>
        [HttpPost("upload-multiple")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadMultipleImages([FromForm] MultipleImagesUploadRequest request)
        {
            if (request.Files == null || !request.Files.Any())
                return BadRequest(new ApiResponse(400, _lang.GetMessage("Image_NoFiles")));

            var imageUrls = await _imageService.UploadImagesAsync(request.Files, request.Folder ?? "general");

            return Ok(new
            {
                success = true,
                message = _lang.GetMessage("Image_MultipleUploaded", imageUrls.Count),
                data = new { imageUrls }
            });
        }

        /// <summary>Uploads up to 5 images for a product. Returns image URLs and thumbnail URLs.</summary>
        [HttpPost("upload-product")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProductImages([FromForm] ProductImagesUploadRequest request)
        {
            if (request.Files == null || !request.Files.Any())
                return BadRequest(new ApiResponse(400, _lang.GetMessage("Image_NoFiles")));

            if (request.Files.Count > 5)
                return BadRequest(new ApiResponse(400, _lang.GetMessage("Image_MaxExceeded")));

            var results = await _imageService.UploadProductImagesAsync(request.Files);

            return Ok(new
            {
                success = true,
                message = _lang.GetMessage("Image_ProductUploaded", results.Count),
                data = new
                {
                    imageUrls = results.Select(r => r.imageUrl).ToList(),
                    thumbnailUrls = results.Select(r => r.thumbnailUrl).ToList()
                }
            });
        }

        /// <summary>Uploads a vendor logo image to the vendors/logos folder.</summary>
        [HttpPost("upload-logo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadVendorLogo([FromForm] SingleFileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new ApiResponse(400, _lang.GetMessage("Image_NoFile")));

            var imageUrl = await _imageService.UploadImageAsync(request.File, "vendors/logos");

            return Ok(new
            {
                success = true,
                message = _lang.GetMessage("Image_LogoUploaded"),
                data = new { imageUrl }
            });
        }

        /// <summary>Uploads a vendor banner image to the vendors/banners folder.</summary>
        [HttpPost("upload-banner")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadVendorBanner([FromForm] SingleFileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new ApiResponse(400, _lang.GetMessage("Image_NoFile")));

            var imageUrl = await _imageService.UploadImageAsync(request.File, "vendors/banners");

            return Ok(new
            {
                success = true,
                message = _lang.GetMessage("Image_BannerUploaded"),
                data = new { imageUrl }
            });
        }

        /// <summary>Deletes an image from the server by its URL path.</summary>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteImage([FromQuery] string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return BadRequest(new ApiResponse(400, _lang.GetMessage("Image_UrlRequired")));

            var deleted = await _imageService.DeleteImageAsync(imageUrl);

            if (!deleted)
                return NotFound(new ApiResponse(404, _lang.GetMessage("Image_NotFound")));

            return Ok(new
            {
                success = true,
                message = _lang.GetMessage("Image_Deleted")
            });
        }
    }

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