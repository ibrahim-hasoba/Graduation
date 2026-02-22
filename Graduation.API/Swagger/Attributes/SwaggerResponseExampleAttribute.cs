using System;
namespace Graduation.API.Swagger.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class SwaggerResponseExampleAttribute : Attribute
    {
        public Type ProviderType { get; }
        public string StatusCode { get; }

        public SwaggerResponseExampleAttribute(string statusCode, Type providerType)
        {
            StatusCode = statusCode;
            ProviderType = providerType;
        }
    }
}
