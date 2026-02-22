using System;
namespace Graduation.API.Swagger.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SwaggerRequestExampleAttribute : Attribute
    {
        public Type ProviderType { get; }

        public SwaggerRequestExampleAttribute(Type providerType)
        {
            ProviderType = providerType;
        }
    }
}
