using Graduation.BLL.Errors;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.Address;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AddressController : BaseController
    {
        private readonly IUnitOfWork _uow;
        private readonly UserManager<AppUser> _userManager;

        private const int MaxAddressesPerUser = 10;

        public AddressController(
            IUnitOfWork uow,
            UserManager<AppUser> userManager,
            ILanguageService lang)
            : base(lang)
        {
            _uow = uow;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetAddresses()
        {
            var userId = _userManager.GetUserId(User);
            var addresses = await _uow.Repository<UserAddress>().Query()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();

            return OkResult(data: addresses.Select(MapToDto));
        }

        [HttpPost]
        public async Task<IActionResult> AddAddress([FromBody] UserAddressDto dto)
        {
            var userId = _userManager.GetUserId(User);
            var count = await _uow.Repository<UserAddress>().Query()
                .CountAsync(a => a.UserId == userId);
            if (count >= MaxAddressesPerUser)
                throw new BadRequestException(Lang.GetMessage(LangKeys.Address.MaxReached, MaxAddressesPerUser));

            var isFirstAddress = count == 0;
            var makeDefault = dto.IsDefault || isFirstAddress;

            var address = await _uow.ExecuteInTransactionAsync(async () =>
            {
                if (makeDefault)
                {
                    await _uow.Repository<UserAddress>().Query()
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

                _uow.Repository<UserAddress>().Add(address);
                await _uow.SaveChangesAsync();
                return address;
            });

            return CreatedResult(data: MapToDto(address), message: Lang.GetMessage(LangKeys.Address.Added));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] UserAddressDto dto)
        {
            var userId = _userManager.GetUserId(User);
            var address = await _uow.Repository<UserAddress>().Query()
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) throw new NotFoundException(Lang.GetMessage(LangKeys.Address.NotFound));

            var updated = await _uow.ExecuteInTransactionAsync(async () =>
            {
                if (dto.IsDefault && !address.IsDefault)
                {
                    await _uow.Repository<UserAddress>().Query()
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

                await _uow.SaveChangesAsync();
                return address;
            });

            return OkResult(data: MapToDto(updated), message: Lang.GetMessage(LangKeys.Address.Updated));
        }

        [HttpPut("{id:int}/default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = _userManager.GetUserId(User);
            var address = await _uow.Repository<UserAddress>().Query()
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) throw new NotFoundException(Lang.GetMessage(LangKeys.Address.NotFound));

            if (address.IsDefault)
                return OkResult(
                    message: Lang.GetMessage(LangKeys.Address.AlreadyDefault),
                    data: MapToDto(address));

            await _uow.ExecuteInTransactionAsync(async () =>
            {
                await _uow.Repository<UserAddress>().Query()
                    .Where(a => a.UserId == userId && a.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));

                address.IsDefault = true;
                address.UpdatedAt = DateTime.UtcNow;
                await _uow.SaveChangesAsync();
            });

            return OkResult(
                message: Lang.GetMessage(LangKeys.Address.DefaultUpdated),
                data: MapToDto(address));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var userId = _userManager.GetUserId(User);
            var address = await _uow.Repository<UserAddress>().Query()
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) throw new NotFoundException(Lang.GetMessage(LangKeys.Address.NotFound));

            var wasDefault = address.IsDefault;

            await _uow.ExecuteInTransactionAsync(async () =>
            {
                _uow.Repository<UserAddress>().Delete(address);

                if (wasDefault)
                {
                    var next = await _uow.Repository<UserAddress>().Query()
                        .Where(a => a.UserId == userId && a.Id != id)
                        .OrderByDescending(a => a.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (next != null)
                        next.IsDefault = true;
                }

                await _uow.SaveChangesAsync();
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
