using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IPaymobService
    {
      
        Task<string> CreatePaymentUrlAsync(
            string orderNumber, decimal amount,
            string firstName, string lastName,
            string email, string phone, string city,
            string clientType = "web");
        bool VerifyHmac(Dictionary<string, string> callbackData, string receivedHmac);
    }
}
