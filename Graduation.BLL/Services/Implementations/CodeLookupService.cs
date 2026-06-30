using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Entities;
using Graduation.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.Errors;

namespace Graduation.BLL.Services.Implementations
{
    public class CodeLookupService : ICodeLookupService
    {
        private readonly IUnitOfWork _uow;

        public CodeLookupService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<string> ResolveUserIdAsync(string code)
        {
            var id = await _uow.Repository<AppUser>().Query()
                .Where(u => u.Code == code)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"User with code '{code}' was not found");

            return id;
        }

        public async Task<int> ResolveProductIdAsync(string code)
        {
            var id = await _uow.Repository<Product>().Query()
                .Where(p => p.Code == code)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"Product with code '{code}' was not found");

            return id.Value;
        }

        public async Task<int> ResolveVendorIdAsync(string code)
        {
            var id = await _uow.Repository<Vendor>().Query()
                .Where(v => v.Code == code)
                .Select(v => (int?)v.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"Vendor with code '{code}' was not found");

            return id.Value;
        }

        public async Task<int> ResolveCategoryIdAsync(string code)
        {
            var id = await _uow.Repository<Category>().Query()
                .Where(c => c.Code == code)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"Category with code '{code}' was not found");

            return id.Value;
        }

        public async Task<int> ResolveOrderIdAsync(string orderNumber)
        {
            var id = await _uow.Repository<Order>().Query()
                .Where(o => o.OrderNumber == orderNumber)
                .Select(o => (int?)o.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"Order with number '{orderNumber}' was not found");

            return id.Value;
        }
    }
}
