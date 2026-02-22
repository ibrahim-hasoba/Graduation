using System;

namespace Graduation.API.Errors
{
    /// <summary>
    /// Standard error response envelope returned by the API when an error occurs.
    /// </summary>
    /// <remarks>
    /// Example JSON (single error):
    /// {
    ///   "statusCode": 400,
    ///   "message": "A Bad Request, You Have Made"
    /// }
    ///
    /// Validation error example (use <see cref="ApiValidationErrorResponse"/> for details):
    /// {
    ///   "statusCode": 400,
    ///   "message": "One or more validation errors occurred.",
    ///   "errors": { "Email": ["Email is required"] }
    /// }
    /// </remarks>
    public class ApiResponse
    {
        /// <summary>HTTP status code for the response.</summary>
        public int StatusCode { get; set; }

        /// <summary>Human-readable error message. May be null when not provided.</summary>
        public string? Message { get; set; }

        /// <summary>Creates an <see cref="ApiResponse"/> with an optional message.</summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="message">Optional message; a default message is used if not provided.</param>
        public ApiResponse(int statusCode, string? message = null)
        {
            StatusCode = statusCode;
            Message = message ?? GetDefaulMessageForStatusCode(statusCode);
        }

        private string GetDefaulMessageForStatusCode(int statusCode)
            => statusCode switch
            {
                400 => "A Bad Request, You Have Made",
                401 => "Authorized, You Are Not",
                404 => "Resource was not found",
                500 => "Errors are the path to the dark side. Errors lead to anger. Anger leads to hate. Hate leads to career change.",
                _ => "An unexpected error occurred."
            };
    }
}
