namespace Graduation.BLL.Common
{
    public class Result
    {
        public bool IsSuccess { get; }
        public string? Error { get; }
        public int StatusCode { get; }

        protected Result(bool isSuccess, string? error, int statusCode = 400)
        {
            IsSuccess = isSuccess;
            Error = error;
            StatusCode = statusCode;
        }

        public static Result Success() => new(true, null);
        public static Result Failure(string error, int statusCode = 400) => new(false, error, statusCode);
        public static Result<T> Success<T>(T data) => new(data, true, null);
        public static Result<T> Failure<T>(string error, int statusCode = 400) => new(default, false, error, statusCode);
    }

    public class Result<T> : Result
    {
        public T? Data { get; }

        public Result(T? data, bool isSuccess, string? error, int statusCode = 400)
            : base(isSuccess, error, statusCode)
        {
            Data = data;
        }
    }
}
