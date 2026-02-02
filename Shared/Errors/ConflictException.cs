namespace Graduation.API.Errors
{
    public class ConflictException : BusinessException
    {
        public ConflictException(string message) : base(message, 409)
        {
        }
    }
}