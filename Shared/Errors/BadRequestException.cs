namespace Shared.Errors
{
    public class BadRequestException : BusinessException
    {
        public BadRequestException(string message) : base(message, 400)
        {
        }
    }
}
