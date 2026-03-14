using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
    public interface ICodeLookupService
    {
        Task<string> ResolveUserIdAsync(string code);
        Task<int> ResolveProductIdAsync(string code);
        Task<int> ResolveVendorIdAsync(string code);
        Task<int> ResolveCategoryIdAsync(string code);
        Task<int> ResolveOrderIdAsync(string orderNumber);
    }
}
