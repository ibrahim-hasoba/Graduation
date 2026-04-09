using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;
using Shared.DTOs.Vendor;
using Shared.Errors;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrandsController : ControllerBase
    {
        private readonly IVendorService _vendorService;

        public BrandsController(IVendorService vendorService)
        {
            _vendorService = vendorService;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllBrands(
                [FromQuery] int pageNumber = 1,
                [FromQuery] int pageSize = 10)
        {
            var brands = await _vendorService.GetPublicVendorsListAsync(pageNumber, pageSize);
            return Ok(new ApiResult(data: brands));
        }


        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBrandDetails(int id)
        {
            var brandDetails = await _vendorService.GetPublicVendorDetailsAsync(id);

            return Ok(new ApiResult(data: brandDetails));
        }
    }
}