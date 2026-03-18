using Shared.DTOs.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IPaymentService
    {
        /// <summary>
        /// Runs the Paymob 3-step flow and returns a PaymentDto
        /// containing the iframe URL the frontend redirects the customer to.
        /// </summary>
        Task<PaymentDto> InitiatePaymentAsync(int orderId);

        /// <summary>
        /// Called by the webhook controller when Paymob sends a callback.
        /// Verifies HMAC, then updates Payment + Order status.
        /// </summary>
        Task HandleWebhookAsync(
            Dictionary<string, string> callbackData,
            string receivedHmac);

        /// <summary>Get payment details by order number (customer view).</summary>
        Task<PaymentDto> GetByOrderNumberAsync(string orderNumber, string userId);

        /// <summary>Admin: paginated list of all payments.</summary>
        Task<PagedPaymentResultDto> GetAllAsync(
            int pageNumber, int pageSize, string? status);
    }
}
