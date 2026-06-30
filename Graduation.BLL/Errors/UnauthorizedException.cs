using Graduation.BLL.Errors;

namespace Graduation.BLL.Errors
{

    public class UnauthorizedException : BusinessException
    {
        public UnauthorizedException(string message = "You are not authorized to perform this action")
            : base(message, 401)
        {
        }
    }
}
