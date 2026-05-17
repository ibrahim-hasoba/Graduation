using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Graduation.API.Errors;
using Microsoft.AspNetCore.Authorization;

namespace Graduation.API.Swagger
{
    public class ApiResponseOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Responses == null) operation.Responses = new OpenApiResponses();

            if (!operation.Responses.ContainsKey("200"))
            {
                var schema = context.SchemaGenerator.GenerateSchema(typeof(ApiResult), context.SchemaRepository);
                operation.Responses["200"] = new OpenApiResponse
                {
                    Description = "Successful response",
                    Content =
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = schema }
                    }
                };
            }

            foreach (var code in new[] { "400", "404", "500" })
            {
                if (!operation.Responses.ContainsKey(code))
                {
                    var errSchema = context.SchemaGenerator.GenerateSchema(typeof(ApiResponse), context.SchemaRepository);
                    operation.Responses[code] = new OpenApiResponse
                    {
                        Description = "Error",
                        Content =
                        {
                            ["application/json"] = new OpenApiMediaType { Schema = errSchema }
                        }
                    };
                }
            }

            var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .Concat(context.MethodInfo.GetCustomAttributes(true))
                .Any(a => a is AuthorizeAttribute) == true;

            if (hasAuthorize && !operation.Responses.ContainsKey("401"))
            {
                var errSchema = context.SchemaGenerator.GenerateSchema(typeof(ApiResponse), context.SchemaRepository);
                operation.Responses["401"] = new OpenApiResponse
                {
                    Description = "Unauthorized",
                    Content =
                    {
                        ["application/json"] = new OpenApiMediaType { Schema = errSchema }
                    }
                };
            }
        }
    }
}
