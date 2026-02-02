// File: Graduation.API/Controllers/VendorsController.cs
using Graduation.API.DTOs.Vendor;
using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VendorsController : ControllerBase
    {
        private readonly IVendorService _vendorService;

        public VendorsController(IVendorService vendorService)
        {
            _vendorService = vendorService;
        }

        /// <summary>
        /// Get all vendors (public access)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllVendors([FromQuery] bool? isApproved = true)
        {
            var vendors = await _vendorService.GetAllVendorsAsync(isApproved);
            return Ok(new { success = true, data = vendors });
        }

        /// <summary>
        /// Get vendor by ID (public access)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetVendorById(int id)
        {
            var vendor = await _vendorService.GetVendorByIdAsync(id);
            return Ok(new { success = true, data = vendor });
        }

        /// <summary>
        /// Get current user's vendor profile
        /// </summary>
        [HttpGet("my-store")]
        [Authorize]
        public async Task<IActionResult> GetMyVendorProfile()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);

            if (vendor == null)
                return NotFound(new ApiResponse(404, "You don't have a vendor account yet"));

            return Ok(new { success = true, data = vendor });
        }

        /// <summary>
        /// Register as a vendor (authenticated users only)
        /// </summary>
        [HttpPost("register")]
        [Authorize]
        public async Task<IActionResult> RegisterVendor([FromBody] VendorRegisterDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.RegisterVendorAsync(userId, dto);

            return StatusCode(201, new
            {
                success = true,
                message = "Vendor registration submitted. Waiting for admin approval.",
                data = vendor
            });
        }

        /// <summary>
        /// Update vendor profile (vendor owner only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateVendor(int id, [FromBody] VendorUpdateDto dto)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authenticated"));

            var vendor = await _vendorService.UpdateVendorAsync(id, userId, dto);
            return Ok(new { success = true, message = "Vendor updated successfully", data = vendor });
        }

        /// <summary>
        /// Approve or reject vendor (admin only)
        /// </summary>
        [HttpPost("{id}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveVendor(int id, [FromBody] VendorApprovalDto dto)
        {
            var vendor = await _vendorService.ApproveVendorAsync(id, dto.IsApproved, dto.RejectionReason);

            var message = dto.IsApproved ? "Vendor approved successfully" : "Vendor rejected";
            return Ok(new { success = true, message, data = vendor });
        }

        /// <summary>
        /// Toggle vendor active status (admin only)
        /// </summary>
        [HttpPatch("{id}/toggle-status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleVendorStatus(int id)
        {
            var vendor = await _vendorService.ToggleVendorStatusAsync(id);
            return Ok(new { success = true, message = "Vendor status updated", data = vendor });
        }

        /// <summary>
        /// Delete vendor (admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteVendor(int id)
        {
            await _vendorService.DeleteVendorAsync(id);
            return Ok(new { success = true, message = "Vendor deleted successfully" });
        }

        /// <summary>
        /// Get pending vendor applications (admin only)
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingVendors()
        {
            var vendors = await _vendorService.GetAllVendorsAsync(isApproved: false);
            return Ok(new { success = true, data = vendors });
        }
    }
}