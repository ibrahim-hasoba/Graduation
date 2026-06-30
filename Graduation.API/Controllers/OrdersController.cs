using Graduation.API.Extensions;
using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Graduation.BLL.DTOs.Order;
using Graduation.BLL.DTOs.ReturnRequest;

namespace Graduation.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : BaseController
    {
        private readonly IOrderService _orderService;
        private readonly IVendorService _vendorService;
        private readonly IPaymentService _paymentService;
        private readonly DatabaseContext _context;

        public OrdersController(
            IOrderService orderService,
            IVendorService vendorService,
            IPaymentService paymentService,
            DatabaseContext context,
            ILanguageService lang)
            : base(lang)
        {
            _orderService = orderService;
            _vendorService = vendorService;
            _paymentService = paymentService;
            _context = context;
        }
        /// <summary>Creates a new order from the authenticated user's cart.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            var userId = GetRequiredUserId();
            var order = await _orderService.CreateOrderAsync(userId, dto);
            return CreatedResult(data: order, message: Lang.GetMessage(LangKeys.Order.Placed));
        }
        /// <summary>Initiates a payment session for an existing order.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{id}/initiate-payment")]
        public async Task<IActionResult> InitiatePayment(int id)
        {
            var userId = GetRequiredUserId();
            var result = await _paymentService.InitiatePaymentAsync(id);
            return OkResult(data: result);
        }
        /// <summary>Gets the payment status for a specific order by order number.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{orderNumber}/payment-status")]
        public async Task<IActionResult> GetPaymentStatus(string orderNumber)
        {
            var userId = GetRequiredUserId();
            var payment = await _paymentService.GetByOrderNumberAsync(orderNumber, userId);

            return OkResult(data: new
            {
                orderNumber,
                paymentStatus = payment.Status,
                paymentMethod = payment.Method,
                amount = payment.Amount,
                paidAt = payment.PaidAt,
                isTerminal = IsTerminalStatus(payment.Status)
            });
        }
        /// <summary>Gets a paginated list of orders for the authenticated user.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetRequiredUserId();
            var pagedOrders = await _orderService.GetUserOrdersAsync(userId, pageNumber, pageSize);
            return OkResult(data: pagedOrders);
        }
        /// <summary>Gets a single order's details by its ID.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var userId = GetRequiredUserId();

            OrderDto result;

            if (User.IsInRole("Admin"))
            {
                result = await _orderService.AdminGetOrderByIdAsync(id);
            }
            else
            {
                var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
                if (vendor != null)
                    result = await _orderService.GetOrderByIdForVendorAsync(id, vendor.Id);
                else
                    result = await _orderService.GetOrderByIdAsync(id, userId);
            }

            return OkResult(data: result);
        }
        /// <summary>Gets all orders containing the authenticated vendor's products.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [HttpGet("vendor")]
        public async Task<IActionResult> GetVendorOrders()
        {
            var userId = GetRequiredUserId();
            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Graduation.BLL.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Order.NotVendor));

            var orders = await _orderService.GetVendorOrdersAsync(vendor.Id);
            return OkResult(data: orders);
        }
        /// <summary>Updates the status of an order.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var userId = GetRequiredUserId();

            if (User.IsInRole("Admin"))
            {
                var orderResult = await _orderService.AdminUpdateOrderStatusAsync(id, dto);
                return OkResult(data: orderResult, message: Lang.GetMessage(LangKeys.Order.StatusUpdated));
            }

            var vendor = await _vendorService.GetVendorByUserIdAsync(userId);
            if (vendor == null)
                throw new Graduation.BLL.Errors.UnauthorizedException(Lang.GetMessage(LangKeys.Order.UpdateNotVendor));

            var vendorResult = await _orderService.UpdateOrderStatusAsync(id, vendor.Id, dto);
            return OkResult(data: vendorResult, message: Lang.GetMessage(LangKeys.Order.StatusUpdated));
        }
        /// <summary>Cancels an order with an optional reason.</summary>
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderDto dto)
        {
            var userId = GetRequiredUserId();
            var order = await _orderService.CancelOrderAsync(
                id, userId, dto.Reason ?? "Cancelled by customer");

            return OkResult(data: order, message: Lang.GetMessage(LangKeys.Order.Cancelled));
        }
        /// <summary>Gets map tracking data for a shipped order.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("{orderNumber}/track")]
        public async Task<IActionResult> GetOrderTracking(string orderNumber)
        {
            var userId = GetRequiredUserId();
            var trackingData = await _orderService.GetOrderMapTrackingAsync(orderNumber, userId);
            return OkResult(data: trackingData);
        }

        /// <summary>Requests a return for a delivered order.</summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpPost("{id}/return-request")]
        public async Task<IActionResult> CreateReturnRequest(int id, [FromBody] CreateReturnRequestDto dto)
        {
            var userId = GetRequiredUserId();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId)
                ?? throw new Graduation.BLL.Errors.NotFoundException("Order", id);

            if (order.Status != DAL.Entities.OrderStatus.Delivered)
                throw new Graduation.BLL.Errors.BadRequestException("Order has not been delivered yet");

            var existingPending = await _context.ReturnRequests
                .AnyAsync(r => r.OrderId == id && r.Status == DAL.Entities.ReturnRequestStatus.Pending);
            if (existingPending)
                throw new Graduation.BLL.Errors.BadRequestException("A pending return request already exists for this order");

            var returnRequest = new DAL.Entities.ReturnRequest
            {
                OrderId = id,
                UserId = userId,
                Reason = dto.Reason,
                Status = DAL.Entities.ReturnRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };

            _context.ReturnRequests.Add(returnRequest);
            await _context.SaveChangesAsync();

            return OkResult(message: "Return request submitted");
        }

        private static bool IsTerminalStatus(string status) =>
            status is "Paid" or "Failed" or "Refunded";
    }

    public class CancelOrderDto
    {
        public string? Reason { get; set; }
    }
}
