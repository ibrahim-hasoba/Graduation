using Graduation.API.Resources;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Reflection;

namespace Graduation.API.Extensions
{
    public static class LocalizationExtensions
    {
        
        public static IServiceCollection AddAppLocalization(this IServiceCollection services)
        {
            services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });

            return services;
        }

        
        public static IApplicationBuilder UseAppLocalization(this IApplicationBuilder app)
        {
            var supportedCultures = new[]
            {
                new CultureInfo("en"),
                new CultureInfo("ar"),
            };

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en"),
                SupportedCultures = supportedCultures,
                SupportedUICultures = supportedCultures,
                RequestCultureProviders = new List<Microsoft.AspNetCore.Localization.IRequestCultureProvider>()
            });

            return app;
        }
    }
}
