using Graduation.API.Errors;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Address;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AddressController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;

        private const int MaxAddressesPerUser = 10;

        public AddressController(DatabaseContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Get all saved addresses for the authenticated user.
        /// Default address is always returned first.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAddresses()
        {
            var userId = _userManager.GetUserId(User);

            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();

            return Ok(new ApiResult(data: addresses.Select(MapToDto)));
        }

        /// <summary>
        /// Add a new saved address. First address is automatically set as default.
        /// Maximum of 10 addresses per user.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddAddress([FromBody] UserAddressDto dto)
        {
            var userId = _userManager.GetUserId(User);

            var count = await _context.UserAddresses.CountAsync(a => a.UserId == userId);
            if (count >= MaxAddressesPerUser)
                throw new BadRequestException($"You can save a maximum of {MaxAddressesPerUser} addresses.");

            var isFirstAddress = count == 0;
            var makeDefault = dto.IsDefault || isFirstAddress;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (makeDefault)
                {
                    await _context.UserAddresses
                        .Where(a => a.UserId == userId && a.IsDefault)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));
                }

                var address = new UserAddress
                {
                    UserId = userId!,
                    Nickname = dto.Nickname.Trim(),
                    FullAddress = dto.FullAddress.Trim(),
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    IsDefault = makeDefault,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.UserAddresses.Add(address);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return StatusCode(201, new ApiResult(
                    message: "Address added successfully.",
                    data: MapToDto(address)));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Update an existing saved address.
        /// Only the owner of the address can update it.
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] UserAddressDto dto)
        {
            var userId = _userManager.GetUserId(User);

            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (address == null)
                throw new NotFoundException("Address not found.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (dto.IsDefault && !address.IsDefault)
                {
                    await _context.UserAddresses
                        .Where(a => a.UserId == userId && a.IsDefault)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));
                }

                address.Nickname = dto.Nickname.Trim();
                address.FullAddress = dto.FullAddress.Trim();
                address.Latitude = dto.Latitude;
                address.Longitude = dto.Longitude;
                address.IsDefault = dto.IsDefault || address.IsDefault;
                address.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResult(
                    message: "Address updated successfully.",
                    data: MapToDto(address)));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Set an address as the default. Clears any previous default.
        /// Called by the "Apply" button on the address list screen.
        /// </summary>
        [HttpPut("{id:int}/default")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = _userManager.GetUserId(User);

            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (address == null)
                throw new NotFoundException("Address not found.");

            if (address.IsDefault)
                return Ok(new ApiResult(
                    message: "This address is already your default.",
                    data: MapToDto(address)));

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.UserAddresses
                    .Where(a => a.UserId == userId && a.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));

                address.IsDefault = true;
                address.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResult(
                    message: "Default address updated successfully.",
                    data: MapToDto(address)));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Delete a saved address. If the deleted address was the default,
        /// the most recently added remaining address is automatically promoted.
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var userId = _userManager.GetUserId(User);

            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (address == null)
                throw new NotFoundException("Address not found.");

            var wasDefault = address.IsDefault;

            _context.UserAddresses.Remove(address);
            await _context.SaveChangesAsync();

            if (wasDefault)
            {
                var next = await _context.UserAddresses
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();

                if (next != null)
                {
                    next.IsDefault = true;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new ApiResult(message: "Address deleted successfully."));
        }

        #region Helpers

        private static AddressResponseDto MapToDto(UserAddress a) => new()
        {
            Id = a.Id,
            Nickname = a.Nickname,
            FullAddress = a.FullAddress,
            Latitude = a.Latitude,
            Longitude = a.Longitude,
            IsDefault = a.IsDefault,
            CreatedAt = a.CreatedAt
        };

        #endregion
    }
}
