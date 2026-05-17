using Graduation.DAL.Data;
using Graduation.API.Errors;
using Graduation.BLL.Services.Interfaces;
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
    public class AddressController : BaseController
    {
        private readonly DatabaseContext _context;
        private readonly UserManager<AppUser> _userManager;

        private const int MaxAddressesPerUser = 10;

        public AddressController(
            DatabaseContext context,
            UserManager<AppUser> userManager,
            ILanguageService lang)
            : base(lang)
        {
            _context = context;
            _userManager = userManager;
        }
        /// <summary>Gets all saved addresses for the authenticated user, ordered by default then creation date.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet]
        public async Task<IActionResult> GetAddresses()
        {
            var userId = _userManager.GetUserId(User);
            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();

            return OkResult(data: addresses.Select(MapToDto));
        }
        /// <summary>Adds a new shipping address. The first address is auto-set as the default.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost]
        public async Task<IActionResult> AddAddress([FromBody] UserAddressDto dto)
        {
            var userId = _userManager.GetUserId(User);
            var count = await _context.UserAddresses.CountAsync(a => a.UserId == userId);
            if (count >= MaxAddressesPerUser)
                throw new BadRequestException(Lang.GetMessage(LangKeys.Address.MaxReached, MaxAddressesPerUser));

            var isFirstAddress = count == 0;
            var makeDefault = dto.IsDefault || isFirstAddress;

            var address = await ExecuteInTransactionAsync(_context, async () =>
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
                    PhoneNumber = dto.PhoneNumber?.Trim(),
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    IsDefault = makeDefault,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                _context.UserAddresses.Add(address);
                await _context.SaveChangesAsync();
                return address;
            });

            return CreatedResult(data: MapToDto(address), message: Lang.GetMessage(LangKeys.Address.Added));
        }
        /// <summary>Updates an existing address for the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] UserAddressDto dto)
        {
            var userId = _userManager.GetUserId(User);
            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) throw new NotFoundException(Lang.GetMessage(LangKeys.Address.NotFound));

            var updated = await ExecuteInTransactionAsync(_context, async () =>
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
                address.PhoneNumber = dto.PhoneNumber?.Trim();

                await _context.SaveChangesAsync();
                return address;
            });

            return OkResult(data: MapToDto(updated), message: Lang.GetMessage(LangKeys.Address.Updated));
        }
        /// <summary>Sets a specific address as the default shipping address.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("{id:int}/default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = _userManager.GetUserId(User);
            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) throw new NotFoundException(Lang.GetMessage(LangKeys.Address.NotFound));

            if (address.IsDefault)
                return OkResult(
                    message: Lang.GetMessage(LangKeys.Address.AlreadyDefault),
                    data: MapToDto(address));

            await ExecuteInTransactionAsync(_context, async () =>
            {
                await _context.UserAddresses
                    .Where(a => a.UserId == userId && a.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));

                address.IsDefault = true;
                address.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            });

            return OkResult(
                message: Lang.GetMessage(LangKeys.Address.DefaultUpdated),
                data: MapToDto(address));
        }
        /// <summary>Deletes an address by ID and promotes the next address as default if needed.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var userId = _userManager.GetUserId(User);
            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) throw new NotFoundException(Lang.GetMessage(LangKeys.Address.NotFound));

            var wasDefault = address.IsDefault;

            await ExecuteInTransactionAsync(_context, async () =>
            {
                _context.UserAddresses.Remove(address);

                if (wasDefault)
                {
                    var next = await _context.UserAddresses
                        .Where(a => a.UserId == userId && a.Id != id)
                        .OrderByDescending(a => a.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (next != null)
                        next.IsDefault = true;
                }

                await _context.SaveChangesAsync();
            });

            return OkResult(message: Lang.GetMessage(LangKeys.Address.Deleted));
        }

        private static AddressResponseDto MapToDto(UserAddress a) => new()
        {
            Id = a.Id,
            Nickname = a.Nickname,
            FullAddress = a.FullAddress,
            Latitude = a.Latitude,
            Longitude = a.Longitude,
            IsDefault = a.IsDefault,
            CreatedAt = a.CreatedAt,
            PhoneNumber = a.PhoneNumber,
        };
    }
}
