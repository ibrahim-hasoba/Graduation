using FluentValidation;
using System.Reflection;

namespace Graduation.API.Extensions
{
    
    public static class ValidatorRegistrationExtensions
    {
       
        public static IServiceCollection AddValidatorsFromAssemblies(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            var validatorInterfaceType = typeof(IValidator<>);

            foreach (var assembly in assemblies)
            {
                var validatorTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericType)
                    .Where(t => t.GetInterfaces().Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == validatorInterfaceType))
                    .ToList();

                foreach (var implementationType in validatorTypes)
                {
                    var validatorInterfaces = implementationType.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == validatorInterfaceType);

                    foreach (var @interface in validatorInterfaces)
                    {
                        services.AddScoped(@interface, implementationType);
                        services.AddScoped(typeof(IValidator), implementationType);
                    }
                }
            }

            return services;
        }
    }
}
