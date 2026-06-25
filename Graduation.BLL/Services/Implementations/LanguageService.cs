using System.Globalization;
using Graduation.BLL.Services.Interfaces;
using Microsoft.Extensions.Localization;

namespace Graduation.BLL.Services.Implementations
{
    public class LanguageService : ILanguageService
    {
        private readonly IStringLocalizer _localizer;

        public LanguageService(IStringLocalizerFactory factory)
        {
            _localizer = factory.Create(typeof(Messages));
        }

        public string GetMessage(string key, params object[] args)
        {
            var raw = _localizer[key].Value;
            return args.Length > 0 ? string.Format(raw, args) : raw;
        }

        public string CurrentLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    }

    public class Messages { }
}