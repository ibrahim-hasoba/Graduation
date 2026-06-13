using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Graduation.API.Controllers
{
    [Route("api/admin/orders")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminOrdersController : BaseController
    {
        private readonly DatabaseContext _context;

        public AdminOrdersController(
            DatabaseContext context,
            ILanguageService lang)
            : base(lang)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllOrders(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] OrderStatus? status = null)
        {
            var query = _context.Orders
                .Include(o => o.User)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            var totalCount = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    id = o.Id,
                    orderNumber = o.OrderNumber,
                    customerName = $"{o.User.FirstName} {o.User.LastName}",
                    customerEmail = o.User.Email,
                    vendorName = string.Join(", ", o.OrderItems
                        .Select(oi => oi.Product.Vendor.StoreName)
                        .Distinct()),
                    vendorEmail = string.Join(", ", o.OrderItems
                        .Select(oi => oi.Product.Vendor.User.Email)
                        .Distinct()),
                    vendorPhoto = o.OrderItems
                        .Select(oi => oi.Product.Vendor.LogoUrl)
                        .FirstOrDefault() ?? "",
                    totalAmount = o.TotalAmount,
                    status = o.Status.ToString(),
                    paymentStatus = o.PaymentStatus.ToString(),
                    orderDate = o.OrderDate,
                    itemsCount = o.OrderItems.Count
                })
                .ToListAsync();

            return OkResult(
                data: PaginatedResponse(orders, totalCount, pageNumber, pageSize));
        }
    }
}
