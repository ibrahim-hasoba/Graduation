using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;
using Graduation.API.Swagger.Attributes;
using Graduation.API.Swagger.Examples;

namespace Graduation.API.Swagger.Filters
{
    public class ExampleOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var method = context.MethodInfo;

            // Request examples
            var reqAttrs = method.GetCustomAttributes(typeof(SwaggerRequestExampleAttribute), true)
                .Cast<SwaggerRequestExampleAttribute>();

            foreach (var attr in reqAttrs)
            {
                if (operation.RequestBody?.Content == null) continue;
                if (!typeof(IExampleProvider).IsAssignableFrom(attr.ProviderType)) continue;

                var provider = Activator.CreateInstance(attr.ProviderType) as IExampleProvider;
                if (provider == null) continue;

                var exampleObj = provider.GetExample();
                var json = JsonSerializer.Serialize(exampleObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                foreach (var media in operation.RequestBody.Content.Values)
                {
                    media.Example = new OpenApiString(json);
                }
            }

            // Response examples
            var respAttrs = method.GetCustomAttributes(typeof(SwaggerResponseExampleAttribute), true)
                .Cast<SwaggerResponseExampleAttribute>();

            foreach (var attr in respAttrs)
            {
                if (!operation.Responses.TryGetValue(attr.StatusCode, out var resp)) continue;
                if (!typeof(IExampleProvider).IsAssignableFrom(attr.ProviderType)) continue;

                var provider = Activator.CreateInstance(attr.ProviderType) as IExampleProvider;
                if (provider == null) continue;

                var exampleObj = provider.GetExample();
                var json = JsonSerializer.Serialize(exampleObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                foreach (var media in resp.Content?.Values ?? Enumerable.Empty<OpenApiMediaType>())
                {
                    media.Example = new OpenApiString(json);
                }
            }
        }
    }
}
