using Graduation.API.Errors;
using System.Net;
using System.Text.Json;

namespace Graduation.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                context.Response.ContentType = "application/json";

                ApiResponse response;

                // Handle custom business exceptions
                if (ex is BusinessException businessEx)
                {
                    context.Response.StatusCode = businessEx.StatusCode;
                    response = new ApiResponse(businessEx.StatusCode, businessEx.Message);
                }
                else
                {
                    // Handle unexpected exceptions
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                    response = _env.IsDevelopment()
                        ? new ApiException(
                            (int)HttpStatusCode.InternalServerError,
                            ex.Message,
                            ex.StackTrace?.ToString())
                        : new ApiException(
                            (int)HttpStatusCode.InternalServerError,
                            "An internal server error occurred");
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(response, options);

                await context.Response.WriteAsync(json);
            }
        }
    }
}