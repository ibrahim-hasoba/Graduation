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
       
        Task<PaymentDto> InitiatePaymentAsync(int orderId , string clientType = "web");

        
        Task HandleWebhookAsync(
            Dictionary<string, string> callbackData,
            string receivedHmac);

        Task<PaymentDto> GetByOrderNumberAsync(string orderNumber, string userId);

        Task<PagedPaymentResultDto> GetAllAsync(
            int pageNumber, int pageSize, string? status);
    }
}
