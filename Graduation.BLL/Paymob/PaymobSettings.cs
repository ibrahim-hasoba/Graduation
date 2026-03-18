using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Paymob
{
    public class PaymobSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string IntegrationId { get; set; } = string.Empty;
        public string IframeId { get; set; } = string.Empty;
        public string HmacSecret { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://accept.paymob.com/api";
        public string IframeBaseUrl { get; set; } = "https://accept.paymob.com/api/acceptance/iframes";
    }
}
