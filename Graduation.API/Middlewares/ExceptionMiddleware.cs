using System.Net;
using System.Text.Json;
using SharedErrors = Shared.Errors;
using ApiErrors = Graduation.API.Errors;

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

                SharedErrors.ApiResponse response;

                if (ex is SharedErrors.BusinessException sharedBizEx)
                {
                    context.Response.StatusCode = sharedBizEx.StatusCode;
                    response = new SharedErrors.ApiResponse(sharedBizEx.StatusCode, sharedBizEx.Message);
                }
                else if (ex is ApiErrors.BusinessException apiBizEx)
                {
                    context.Response.StatusCode = apiBizEx.StatusCode;
                    response = new SharedErrors.ApiResponse(apiBizEx.StatusCode, apiBizEx.Message);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                    response = _env.IsDevelopment()
                        ? new SharedErrors.ApiException(
                            (int)HttpStatusCode.InternalServerError,
                            ex.Message,
                            ex.StackTrace)
                        : new SharedErrors.ApiException(
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
