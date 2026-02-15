namespace Shared.Errors
{
    public class BusinessException : Exception
    {
        public int StatusCode { get; set; }

        public BusinessException(string message, int statusCode = 400) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}