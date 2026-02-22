using System;

namespace Graduation.API.Errors
{
  /// <summary>
  /// Standard success response envelope returned by controllers.
  /// </summary>
  /// <remarks>
  /// Example JSON:
  /// {
  ///   "success": true,
  ///   "data": { /* resource or list */ },
  ///   "message": "Optional human readable message",
  ///   "count": 12
  /// }
  /// </remarks>
  public class ApiResult
  {
    /// <summary>Indicates the request was successful.</summary>
    public bool Success { get; } = true;

    /// <summary>The returned payload. May be an object, list or null.</summary>
    public object? Data { get; }

    /// <summary>Optional human-readable message for clients.</summary>
    public string? Message { get; }

    /// <summary>Optional count for collections (e.g. total items).</summary>
    public int? Count { get; }

    /// <summary>Creates an <see cref="ApiResult"/>.</summary>
    /// <param name="data">The payload to return.</param>
    /// <param name="message">Optional message for clients.</param>
    /// <param name="count">Optional total/count metadata.</param>
    public ApiResult(object? data = null, string? message = null, int? count = null)
    {
      Data = data;
      Message = message;
      Count = count;
    }
  }
}
