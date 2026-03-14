using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Implementations
{
    public class CodeAssignmentService : ICodeAssignmentService
    {
        private readonly DatabaseContext _context;

        public CodeAssignmentService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task AssignUserCodeAsync(AppUser user)
        {
            if (!string.IsNullOrEmpty(user.Code)) return;
            user.Code = BusinessCodeGenerator.ForUser(user.Id);
            await _context.SaveChangesAsync();
        }

        public async Task AssignProductCodeAsync(Product product)
        {
            if (!string.IsNullOrEmpty(product.Code)) return;
            product.Code = BusinessCodeGenerator.ForProduct(product.Id);
            await _context.SaveChangesAsync();
        }

        public async Task AssignVendorCodeAsync(Vendor vendor)
        {
            if (!string.IsNullOrEmpty(vendor.Code)) return;
            vendor.Code = BusinessCodeGenerator.ForVendor(vendor.Id);
            await _context.SaveChangesAsync();
        }

        public async Task AssignCategoryCodeAsync(Category category)
        {
            if (!string.IsNullOrEmpty(category.Code)) return;
            category.Code = BusinessCodeGenerator.ForCategory(category.Id);
            await _context.SaveChangesAsync();
        }
    }
}
