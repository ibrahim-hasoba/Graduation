using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Graduation.BLL.Utilities;

namespace Graduation.BLL.Services.Implementations
{
    public class CodeAssignmentService : ICodeAssignmentService
    {
        private readonly IUnitOfWork _uow;

        public CodeAssignmentService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task AssignUserCodeAsync(AppUser user)
        {
            if (!string.IsNullOrEmpty(user.Code)) return;
            user.Code = BusinessCodeGenerator.ForUser(user.Id);
            await _uow.SaveChangesAsync();
        }

        public async Task AssignProductCodeAsync(Product product)
        {
            if (!string.IsNullOrEmpty(product.Code)) return;
            product.Code = BusinessCodeGenerator.ForProduct(product.Id);
            await _uow.SaveChangesAsync();
        }

        public async Task AssignVendorCodeAsync(Vendor vendor)
        {
            if (!string.IsNullOrEmpty(vendor.Code)) return;
            vendor.Code = BusinessCodeGenerator.ForVendor(vendor.Id);
            await _uow.SaveChangesAsync();
        }

        public async Task AssignCategoryCodeAsync(Category category)
        {
            if (!string.IsNullOrEmpty(category.Code)) return;
            category.Code = BusinessCodeGenerator.ForCategory(category.Id);
            await _uow.SaveChangesAsync();
        }
    }
}
