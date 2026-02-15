using Shared.Errors;

namespace Shared.Errors
{
    // FIXED: Was returning 403 (Forbidden) which is semantically wrong for authentication failures.
    // 401 = Unauthenticated (wrong credentials, invalid token)
    // 403 = Authenticated but not permitted (use BusinessException with 403 for that case)
    public class UnauthorizedException : BusinessException
    {
        public UnauthorizedException(string message = "You are not authorized to perform this action")
            : base(message, 401)
        {
        }
    }
}
