using Graduation.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
    public interface ICodeAssignmentService
    {
        Task AssignUserCodeAsync(AppUser user);
        Task AssignProductCodeAsync(Product product);
        Task AssignVendorCodeAsync(Vendor vendor);
        Task AssignCategoryCodeAsync(Category category);
    }
}
