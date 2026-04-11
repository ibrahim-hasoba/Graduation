using Graduation.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace Graduation.BLL.Services.Implementations
{
    public class LanguageService : ILanguageService
    {
        private readonly IStringLocalizer _localizer;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LanguageService(
            IStringLocalizerFactory factory,
            IHttpContextAccessor httpContextAccessor)
        {
            // This will look for Messages.resx files in the same namespace as the Messages class
            _localizer = factory.Create(typeof(Messages));
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetMessage(string key, params object[] args)
        {
            var raw = _localizer[key].Value;
            return args.Length > 0 ? string.Format(raw, args) : raw;
        }

        public string CurrentLanguage
        {
            get
            {
                var lang = _httpContextAccessor.HttpContext?
                    .Request.Headers["Accept-Language"]
                    .ToString()
                    .ToLower() ?? "en";
                return lang.StartsWith("ar") ? "ar" : "en";
            }
        }
    }

    // This is the Messages class that matches your resource files
    public class Messages { }
}