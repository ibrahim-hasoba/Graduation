using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
    public interface IPaymobService
    {
        /// <summary>
        /// Full Paymob 3-step flow:
        /// 1. Authenticate → get auth token
        /// 2. Register order → get Paymob order ID
        /// 3. Get payment key → build iframe URL
        /// Returns the iframe URL to redirect the customer to.
        /// </summary>
        Task<string> CreatePaymentUrlAsync(
            string orderNumber,
            decimal amount,
            string firstName,
            string lastName,
            string email,
            string phone,
            string city);

        /// <summary>
        /// Verifies the HMAC signature on Paymob's webhook callback.
        /// Returns true if the signature is valid.
        /// </summary>
        bool VerifyHmac(Dictionary<string, string> callbackData, string receivedHmac);
    }
}
