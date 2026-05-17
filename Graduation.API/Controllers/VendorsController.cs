using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.DTOs.Order;
using Shared.DTOs.Vendor;
using Graduation.API.Extensions;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Vendor")]
    public class VendorsController : BaseController
    {
        private readonly IVendorService _vendorService;
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        private readonly DatabaseContext _context;

        public VendorsController(
            IVendorService vendorService,
            IOrderService orderService,
            IProductService productService,
            DatabaseContext context,
            ILanguageService lang)
            : base(lang)
        {
            _vendorService = vendorService;
            _orderService = orderService;
            _productService = productService;
            _context = context;
        }
        /// <summary>Gets the authenticated vendor's own profile details.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyVendorProfile()
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            return OkResult(data: vendor);
        }
        /// <summary>Updates the authenticated vendor's own profile information.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyVendorProfile([FromBody] VendorUpdateDto dto)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            var updated = await _vendorService.UpdateVendorAsync(vendor.Id, userId, dto);
            return OkResult(data: updated);
        }
        /// <summary>Gets a list of orders containing the authenticated vendor's products.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("orders")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            var orders = await _orderService.GetVendorOrdersAsync(vendor.Id);
            return OkResult(data: orders);
        }
        /// <summary>Gets the full details of a specific order that contains the vendor's products.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("orders/{orderId:int}")]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

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
                throw new Shared.Errors.NotFoundException("Order", orderId);

            return OkResult(data: order);
        }
        /// <summary>Updates the status of an order for the vendor's products.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPut("orders/{orderId:int}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, [FromBody] UpdateOrderStatusDto dto)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            var updated = await _orderService.UpdateOrderStatusAsync(orderId, vendor.Id, dto);
            return OkResult(data: updated);
        }
        /// <summary>Gets the status change timeline for a vendor's order.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("orders/{orderId:int}/timeline")]
        public async Task<IActionResult> GetOrderTimeline(int orderId)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId
                    && o.OrderItems.Any(oi => oi.Product.VendorId == vendor.Id));

            if (order == null)
                throw new Shared.Errors.NotFoundException("Order", orderId);

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

            return OkResult(data: new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                currentStatus = order.Status.ToString(),
                timeline
            });
        }
        /// <summary>Gets the authenticated vendor's own products with pagination.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("products")]
        public async Task<IActionResult> GetMyProducts()
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            var products = await _productService.GetVendorProductsAsync(vendor.Id);
            return OkResult(data: products);
        }
        /// <summary>Gets statistics for the authenticated vendor including products, orders, and revenue.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("stats")]
        public async Task<IActionResult> GetVendorStats()
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

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

            return OkResult(data: new
            {
                totalProducts,
                totalOrders,
                totalRevenue,
                pendingOrders
            });
        }
        /// <summary>Updates the real-time location of a shipped order for customer tracking.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("orders/{orderId:int}/location")]
        public async Task<IActionResult> PostOrderLocation(int orderId, [FromBody] UpdateLocationDto dto)
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Shared.Errors.NotFoundException(Lang.GetMessage(LangKeys.Vendor.NotFound));

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId
                    && o.OrderItems.Any(oi => oi.Product.VendorId == vendor.Id));

            if (order == null)
                throw new Shared.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Vendor.OrderNotFound));

            if (order.Status != OrderStatus.Shipped)
                return BadRequest(new Errors.ApiResult(message: Lang.GetMessage(LangKeys.Order.LocationShippedOnly)));

            await _vendorService.UpdateOrderLocationAsync(orderId, dto.Latitude, dto.Longitude);

            return OkResult(message: Lang.GetMessage(LangKeys.Order.LocationUpdated));
        }
    }

    public class UpdateLocationDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}