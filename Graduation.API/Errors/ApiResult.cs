using System;

namespace Graduation.API.Errors
{
  
  public class ApiResult
  {
    public bool Success { get; } = true;
    public object? Data { get; }
    public string? Message { get; }
    public int? Count { get; }
    public ApiResult(object? data = null, string? message = null, int? count = null)
    {
      Data = data;
      Message = message;
      Count = count;
    }
  }
}
