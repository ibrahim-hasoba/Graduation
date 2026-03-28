using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Data;
using Graduation.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.DTOs.Payment;
using Shared.Errors;
using System.Security.Cryptography;
using System.Text;

namespace Graduation.BLL.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly DatabaseContext _context;
        private readonly IPaymobService _paymob;
        private readonly INotificationService _notifications;
        private readonly IEmailService _emailService;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            DatabaseContext context,
            IPaymobService paymob,
            INotificationService notifications,
            IEmailService emailService,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _paymob = paymob;
            _notifications = notifications;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<PaymentDto> InitiatePaymentAsync(int orderId, string clientType = "web")
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == orderId)
                ?? throw new NotFoundException("Order", orderId);

            if (order.PaymentMethod == PaymentMethod.CashOnDelivery)
                return new PaymentDto
                {
                    OrderNumber = order.OrderNumber,
                    Method = "CashOnDelivery",
                    Status = "Pending",
                    Amount = order.TotalAmount,
                    CreatedAt = DateTime.UtcNow
                };

            var existing = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId);

            if (existing != null)
            {
                if (existing.Status == PaymentStatus2.Paid)
                    return MapToDto(existing, order.OrderNumber);

                if (existing.Status == PaymentStatus2.Pending)
                    return MapToDto(existing, order.OrderNumber);

                existing.PaymentUrl = await _paymob.CreatePaymentUrlAsync(
                    order.OrderNumber,
                    order.TotalAmount,
                    order.ShippingFirstName,
                    order.ShippingLastName,
                    order.User?.Email ?? "guest@heka.com",
                    order.ShippingPhone,
                    order.ShippingCity,
                    clientType);

                existing.Status = PaymentStatus2.Pending;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return MapToDto(existing, order.OrderNumber);
            }

            var paymentUrl = await _paymob.CreatePaymentUrlAsync(
                order.OrderNumber,
                order.TotalAmount,
                order.ShippingFirstName,
                order.ShippingLastName,
                order.User?.Email ?? "guest@heka.com",
                order.ShippingPhone,
                order.ShippingCity,
                clientType);

            var payment = new Payment
            {
                OrderId = orderId,
                Method = order.PaymentMethod,
                Status = PaymentStatus2.Pending,
                Amount = order.TotalAmount,
                PaymentUrl = paymentUrl,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            payment.Code = BuildCode(payment.Id);
            await _context.SaveChangesAsync();

            return MapToDto(payment, order.OrderNumber);
        }

        public async Task HandleWebhookAsync(
            Dictionary<string, string> callbackData,
            string receivedHmac)
        {
            if (!_paymob.VerifyHmac(callbackData, receivedHmac))
            {
                _logger.LogWarning("Invalid HMAC");
                return;
            }

            callbackData.TryGetValue("success", out var successStr);
            callbackData.TryGetValue("id", out var transactionId);
            callbackData.TryGetValue("merchant_order_id", out var orderNumber);
            callbackData.TryGetValue("is_refund", out var isRefundStr);

            var isSuccess = string.Equals(successStr, "true", StringComparison.OrdinalIgnoreCase);
            var isRefund = string.Equals(isRefundStr, "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(orderNumber))
                return;

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderNumber}", orderNumber);
                return;
            }

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == order.Id);

            if (payment == null)
            {
                _logger.LogWarning("Payment not found for order: {OrderNumber}", orderNumber);
                return;
            }

            if (!string.IsNullOrEmpty(transactionId) &&
                payment.PaymobTransactionId == transactionId)
            {
                _logger.LogInformation("Duplicate webhook ignored");
                return;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.Entry(payment).ReloadAsync();

                payment.PaymobTransactionId = transactionId;
                payment.IsSuccess = isSuccess;
                payment.UpdatedAt = DateTime.UtcNow;

                if (isRefund)
                {
                    payment.Status = PaymentStatus2.Refunded;
                    order.PaymentStatus = PaymentStatus.Refunded;

                    await _notifications.CreateNotificationAsync(
                        order.UserId,
                        "Payment Refunded",
                        $"Order {orderNumber} refunded",
                        "Payment",
                        order.Id);
                }
                else if (isSuccess)
                {
                    payment.Status = PaymentStatus2.Paid;
                    payment.PaidAt = DateTime.UtcNow;

                    order.PaymentStatus = PaymentStatus.Paid;
                    order.Status = OrderStatus.Confirmed;
                    order.ConfirmedAt = DateTime.UtcNow;

                    await _notifications.CreateNotificationAsync(
                        order.UserId,
                        "Payment Successful ✅",
                        $"Order {orderNumber} confirmed",
                        "Payment",
                        order.Id);

                    var user = await _context.Users.FindAsync(order.UserId);
                    if (user?.Email != null)
                    {
                        try
                        {
                            await _emailService.SendOrderConfirmationEmailAsync(
                                user.Email,
                                orderNumber,
                                order.TotalAmount);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Email failed");
                        }
                    }
                }
                else
                {
                    payment.Status = PaymentStatus2.Failed;

                    await _notifications.CreateNotificationAsync(
                        order.UserId,
                        "Payment Failed ❌",
                        $"Order {orderNumber} failed",
                        "Payment",
                        order.Id);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Webhook transaction failed");
            }
        }

        public async Task<PaymentDto> GetByOrderNumberAsync(string orderNumber, string userId)
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.UserId == userId)
                ?? throw new NotFoundException($"Order '{orderNumber}' not found");

            if (order.PaymentMethod == PaymentMethod.CashOnDelivery)
                return new PaymentDto
                {
                    OrderNumber = order.OrderNumber,
                    Method = "CashOnDelivery",
                    Status = "Pending",
                    Amount = order.TotalAmount,
                    CreatedAt = order.OrderDate
                };

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == order.Id)
                ?? throw new NotFoundException("Payment record not found");

            return MapToDto(payment, orderNumber);
        }

        public async Task<PagedPaymentResultDto> GetAllAsync(
            int pageNumber, int pageSize, string? status)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Payments
                .Include(p => p.Order)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<PaymentStatus2>(status, ignoreCase: true, out var statusEnum))
                query = query.Where(p => p.Status == statusEnum);

            var totalCount = await query.CountAsync();

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedPaymentResultDto
            {
                Payments = payments.Select(p => MapToDto(p, p.Order.OrderNumber)).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        private static string BuildCode(int id)
        {
            var bytes = Encoding.UTF8.GetBytes(id.ToString());
            var hash = MD5.HashData(bytes);
            return $"PAY-{Convert.ToHexString(hash)[..6]}";
        }

        private static PaymentDto MapToDto(Payment p, string orderNumber) => new()
        {
            PaymentCode = p.Code ?? BuildCode(p.Id),
            OrderNumber = orderNumber,
            Method = p.Method.ToString(),
            Status = p.Status.ToString(),
            Amount = p.Amount,
            PaymentUrl = p.PaymentUrl,
            PaymobTransactionId = p.PaymobTransactionId,
            CreatedAt = p.CreatedAt,
            PaidAt = p.PaidAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}