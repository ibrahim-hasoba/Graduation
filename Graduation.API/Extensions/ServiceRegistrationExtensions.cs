using Graduation.BLL.JwtFeatures;
using Graduation.BLL.Paymob;
using Graduation.BLL.Services.Implementations;
using Graduation.BLL.Services.Interfaces;
using Graduation.DAL.Repositories;
using Scrutor;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceRegistrationExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Scan(scan => scan
                .FromAssembliesOf(typeof(IVendorService))
                .AddClasses(classes => classes.InNamespaces(
                    "Graduation.BLL.Services.Interfaces",
                    "Graduation.BLL.Services.Implementations",
                    "Graduation.BLL.JwtFeatures")
)
                .UsingRegistrationStrategy(RegistrationStrategy.Append)
                .AsMatchingInterface()
                .WithScopedLifetime());

            services.AddScoped<JwtHandler>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddHttpClient<IPaymobService, PaymobService>();

            services.Configure<PaymobSettings>(configuration.GetSection("PaymobSettings"));

            return services;
        }
    }
}
