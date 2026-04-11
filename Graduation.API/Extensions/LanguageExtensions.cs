namespace Graduation.API.Extensions
{
    public static class LanguageExtensions
    {
        
        public static string Localize(this HttpRequest request, string? en, string? ar)
        {
            var lang = request.Headers["Accept-Language"].ToString().ToLower();
            return lang.StartsWith("ar")
                ? (ar ?? en ?? string.Empty)
                : (en ?? ar ?? string.Empty);
        }
    }
}
