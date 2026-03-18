using System.Text.Json.Serialization;

namespace Graduation.BLL.Paymob
{
    public class PaymobOrderResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
    }
}
