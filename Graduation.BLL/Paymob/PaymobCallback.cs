using System.Text.Json.Serialization;

namespace Graduation.BLL.Paymob
{
    public class PaymobCallback
    {
        [JsonPropertyName("obj")]
        public PaymobCallbackObj? Obj { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}
