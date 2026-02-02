using Graduation.API.DTOs.Vendor;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IVendorService
    {
        Task<VendorDto> RegisterVendorAsync(string userId, VendorRegisterDto dto);
        Task<VendorDto> GetVendorByIdAsync(int id);
        Task<VendorDto?> GetVendorByUserIdAsync(string userId);
        Task<IEnumerable<VendorListDto>> GetAllVendorsAsync(bool? isApproved = null);
        Task<VendorDto> UpdateVendorAsync(int id, string userId, VendorUpdateDto dto);
        Task<VendorDto> ApproveVendorAsync(int id, bool isApproved, string? rejectionReason = null);
        Task<VendorDto> ToggleVendorStatusAsync(int id);
        Task DeleteVendorAsync(int id);
    }
}