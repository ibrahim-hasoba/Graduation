using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Order;
using Shared.DTOs.Vendor;
using Shared.Errors;
using System.Security.Claims;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Vendor")]
    public class VendorsController : ControllerBase
    {
        private readonly IVendorService _vendorService;
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        private readonly DatabaseContext _context;

        public VendorsController(
            IVendorService vendorService,
            IOrderService orderService,
            IProductService productService,
            DatabaseContext context)
        {
            _vendorService = vendorService;
            _orderService = orderService;
            _productService = productService;
            _context = context;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyVendorProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("userId");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId!);
            if (vendor == null)
                throw new NotFoundException("Vendor profile not found");

            return Ok(new ApiResult(data: vendor));
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyVendorProfile([FromBody] VendorUpdateDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("userId");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId!);
            if (vendor == null)
                throw new NotFoundException("Vendor profile not found");

            var updated = await _vendorService.UpdateVendorAsync(vendor.Id, userId!, dto);
            return Ok(new ApiResult(data: updated));
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("userId");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId!);
            if (vendor == null)
                throw new NotFoundException("Vendor profile not found");

            var orders = await _orderService.GetVendorOrdersAsync(vendor.Id);
            return Ok(new ApiResult(data: orders));
        }

        [HttpGet("orders/{orderId:int}")]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("userId");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId!);
            if (vendor == null)
                throw new NotFoundException("Vendor profile not found");

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Vendor)
                .FirstOrDefaultAsync(o => o.Id == orderId
                    && o.OrderItems.Any(oi => oi.Product.VendorId == vendor.Id));

            if (order == null)
                throw new NotFoundException("Order", orderId);

            return Ok(new ApiResult(data: order));
        }

        [HttpPut("orders/{orderId:int}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, [FromBody] UpdateOrderStatusDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("userId");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId!);
            if (vendor == null)
                throw new NotFoundException("Vendor profile not found");

            var updated = await _orderService.UpdateOrderStatusAsync(orderId, vendor.Id, dto);
            return Ok(new ApiResult(data: updated));
        }


        [HttpGet("orders/{orderId:int}/timeline")]
        public async Task<IActionResult> GetOrderTimeline(int orderId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("userId");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId!);
            if (vendor == null)
                throw new NotFoundException("Vendor profile not found");

            // Verify vendor ownership and filter by vendor's products
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId
                    && o.OrderItems.Any(oi => oi.Product.VendorId == vendor.Id));

            if (order == null)
                throw new NotFoundException("Order", orderId);

            var timeline = new List<object>();

            timeline.Add(new { status = "Pending", date = order.OrderDate, label = "Order Placed" });

            if (order.ConfirmedAt.HasValue)
                timeline.Add(new { status = "Confirmed", date = order.ConfirmedAt, label = "Order Confirmed" });

            if (order.ShippedAt.HasValue)
                timeline.Add(new { status = "Shipped", date = order.ShippedAt, label = "Order Shipped" });

            if (order.DeliveredAt.HasValue)
                timeline.Add(new { status = "Delivered", date = order.DeliveredAt, label = "Order Delivered" });

            if (order.CancelledAt.HasValue)
                timeline.Add(new
                {
                    status = "Cancelled",
                    date = order.CancelledAt,
                    label = "Order Cancelled",
                    reason = order.CancellationReason
                });

            return Ok(new ApiResult(data: new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                currentStatus = order.Status.ToString(),
                timeline
            }));
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetMyProducts()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("userId");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId!);
            if (vendor == null)
                throw new NotFoundException("Vendor profile not found");

            var products = await _productService.GetVendorProductsAsync(vendor.Id);
            return Ok(new ApiResult(data: products));
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetVendorStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("userId");

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId!);
            if (vendor == null)
                throw new NotFoundException("Vendor profile not found");

            var totalProducts = await _context.Products
                .CountAsync(p => p.VendorId == vendor.Id && p.IsActive);

            var totalOrders = await _context.Orders
                .CountAsync(o => o.OrderItems.Any(oi => oi.Product.VendorId == vendor.Id));

            var totalRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.Delivered
                         && o.OrderItems.Any(oi => oi.Product.VendorId == vendor.Id))
                .SelectMany(o => o.OrderItems.Where(oi => oi.Product.VendorId == vendor.Id))
                .SumAsync(oi => oi.TotalPrice);

            var pendingOrders = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Pending
                              && o.OrderItems.Any(oi => oi.Product.VendorId == vendor.Id));

            return Ok(new ApiResult(data: new
            {
                totalProducts,
                totalOrders,
                totalRevenue,
                pendingOrders
            }));
        }
    }
}
