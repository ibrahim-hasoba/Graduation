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
        private readonly IEmailService _emailService; // ← ADDED
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            DatabaseContext context,
            IPaymobService paymob,
            INotificationService notifications,
            IEmailService emailService, // ← ADDED
            ILogger<PaymentService> logger)
        {
            _context = context;
            _paymob = paymob;
            _notifications = notifications;
            _emailService = emailService; // ← ADDED
            _logger = logger;
        }

        // ── Initiate ──────────────────────────────────────────────────────────

        public async Task<PaymentDto> InitiatePaymentAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == orderId)
                ?? throw new NotFoundException("Order", orderId);

            // Cash on delivery → no Paymob needed
            if (order.PaymentMethod == PaymentMethod.CashOnDelivery)
                return new PaymentDto
                {
                    OrderNumber = order.OrderNumber,
                    Method = "CashOnDelivery",
                    Status = "Pending",
                    Amount = order.TotalAmount,
                    CreatedAt = DateTime.UtcNow
                };

            // Avoid creating duplicate payment records
            var existing = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId);
            if (existing != null)
                return MapToDto(existing, order.OrderNumber);

            // Run the Paymob 3-step flow
            var paymentUrl = await _paymob.CreatePaymentUrlAsync(
                orderNumber: order.OrderNumber,
                amount: order.TotalAmount,
                firstName: order.ShippingFirstName,
                lastName: order.ShippingLastName,
                email: order.User?.Email ?? "guest@heka.com",
                phone: order.ShippingPhone,
                city: order.ShippingCity);

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

            _logger.LogInformation(
                "Payment initiated for order {OrderNumber}, URL generated",
                order.OrderNumber);

            return MapToDto(payment, order.OrderNumber);
        }

        // ── Webhook ───────────────────────────────────────────────────────────

        public async Task HandleWebhookAsync(
            Dictionary<string, string> callbackData,
            string receivedHmac)
        {
            // 1. Verify HMAC signature
            if (!_paymob.VerifyHmac(callbackData, receivedHmac))
            {
                _logger.LogWarning("Paymob webhook: invalid HMAC — ignoring");
                return;
            }

            // 2. Extract key fields
            callbackData.TryGetValue("success", out var successStr);
            callbackData.TryGetValue("id", out var transactionId);
            callbackData.TryGetValue("merchant_order_id", out var orderNumber);
            callbackData.TryGetValue("is_refund", out var isRefundStr);

            var isSuccess = successStr?.ToLower() == "true";
            var isRefund = isRefundStr?.ToLower() == "true";

            if (string.IsNullOrEmpty(orderNumber))
            {
                _logger.LogWarning("Paymob webhook: missing merchant_order_id");
                return;
            }

            // 3. Find the order
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null)
            {
                _logger.LogWarning("Paymob webhook: order {OrderNumber} not found", orderNumber);
                return;
            }

            // 4. Find payment record
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == order.Id);

            if (payment == null)
            {
                _logger.LogWarning(
                    "Paymob webhook: payment record for order {OrderNumber} not found", orderNumber);
                return;
            }

            // 5. Avoid processing the same transaction twice
            if (payment.Status == PaymentStatus2.Paid && !isRefund)
                return;

            payment.PaymobTransactionId = transactionId;
            payment.IsSuccess = isSuccess;
            payment.UpdatedAt = DateTime.UtcNow;

            if (isRefund)
            {
                payment.Status = PaymentStatus2.Refunded;
                order.PaymentStatus = PaymentStatus.Refunded;

                await _notifications.CreateNotificationAsync(
                    userId: order.UserId,
                    title: "Payment Refunded",
                    message: $"Your payment for order {orderNumber} has been refunded.",
                    type: "Payment",
                    orderId: order.Id);
            }
            else if (isSuccess)
            {
                payment.Status = PaymentStatus2.Paid;
                payment.PaidAt = DateTime.UtcNow;
                order.PaymentStatus = PaymentStatus.Paid;
                order.Status = OrderStatus.Confirmed;
                order.ConfirmedAt = DateTime.UtcNow;

                await _notifications.CreateNotificationAsync(
                    userId: order.UserId,
                    title: "Payment Successful ✅",
                    message: $"Your payment for order {orderNumber} was successful! We are preparing your order.",
                    type: "Payment",
                    orderId: order.Id);

                // ── Send confirmation email AFTER successful payment ────────────
                var user = await _context.Users.FindAsync(order.UserId);
                if (user?.Email != null)
                {
                    var capturedOrderNumber = orderNumber;
                    var capturedAmount = order.TotalAmount;
                    var capturedEmail = user.Email;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendOrderConfirmationEmailAsync(
                                capturedEmail, capturedOrderNumber, capturedAmount);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Failed to send payment confirmation email for order {OrderNumber}",
                                capturedOrderNumber);
                        }
                    });
                }

                _logger.LogInformation(
                    "Payment confirmed for order {OrderNumber}, transaction {TransactionId}",
                    orderNumber, transactionId);
            }
            else
            {
                payment.Status = PaymentStatus2.Failed;

                await _notifications.CreateNotificationAsync(
                    userId: order.UserId,
                    title: "Payment Failed ❌",
                    message: $"Your payment for order {orderNumber} failed. Please try again.",
                    type: "Payment",
                    orderId: order.Id);

                _logger.LogWarning(
                    "Payment failed for order {OrderNumber}, transaction {TransactionId}",
                    orderNumber, transactionId);
            }

            await _context.SaveChangesAsync();
        }

        // ── Get by order number ───────────────────────────────────────────────

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

        // ── Admin: all payments ───────────────────────────────────────────────

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

        // ── Helpers ───────────────────────────────────────────────────────────

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
