namespace Graduation.API.Errors
{
    public class NotFoundException : BusinessException
    {
        public NotFoundException(string message) : base(message, 404)
        {
        }

        public NotFoundException(string resourceName, object key)
            : base($"{resourceName} with id '{key}' was not found", 404)
        {
        }
    }
}