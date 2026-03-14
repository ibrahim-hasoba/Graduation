using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Implementations
{
    public class CodeLookupService : ICodeLookupService
    {
        private readonly DatabaseContext _context;

        public CodeLookupService(DatabaseContext context)
        {
            _context = context;
        }


        public async Task<string> ResolveUserIdAsync(string code)
        {
            var id = await _context.Users
                .Where(u => u.Code == code)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"User with code '{code}' was not found");

            return id;
        }


        public async Task<int> ResolveProductIdAsync(string code)
        {
            var id = await _context.Products
                .Where(p => p.Code == code)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"Product with code '{code}' was not found");

            return id.Value;
        }


        public async Task<int> ResolveVendorIdAsync(string code)
        {
            var id = await _context.Vendors
                .Where(v => v.Code == code)
                .Select(v => (int?)v.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"Vendor with code '{code}' was not found");

            return id.Value;
        }


        public async Task<int> ResolveCategoryIdAsync(string code)
        {
            var id = await _context.Categories
                .Where(c => c.Code == code)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"Category with code '{code}' was not found");

            return id.Value;
        }


        public async Task<int> ResolveOrderIdAsync(string orderNumber)
        {
            var id = await _context.Orders
                .Where(o => o.OrderNumber == orderNumber)
                .Select(o => (int?)o.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new NotFoundException($"Order with number '{orderNumber}' was not found");

            return id.Value;
        }
    }
}
