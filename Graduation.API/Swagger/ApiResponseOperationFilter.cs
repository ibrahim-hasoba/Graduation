using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Graduation.API.Errors;

namespace Graduation.API.Swagger
{
  /// <summary>
  /// Adds common response schemas (ApiResult/ApiResponse) to operations.
  /// </summary>
  public class ApiResponseOperationFilter : IOperationFilter
  {
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
      if (operation.Responses == null) operation.Responses = new OpenApiResponses();

      // Success response (200) - ApiResult
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

      // Common error responses
      foreach (var code in new[] { "400", "401", "404", "500" })
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

      // Leave room for per-endpoint examples (ExampleOperationFilter) to override the generic example.
    }
  }
}
