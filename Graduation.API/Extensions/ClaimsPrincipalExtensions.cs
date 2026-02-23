using System.Security.Claims;

namespace Graduation.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("userId");
        }

        public static string? GetUserEmail(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.Email);

        public static string? GetUserName(this ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.Name);

        public static IEnumerable<string> GetUserRoles(this ClaimsPrincipal user)
            => user.FindAll(ClaimTypes.Role).Select(c => c.Value);

        public static bool IsInAnyRole(this ClaimsPrincipal user, params string[] roles)
            => roles.Any(user.IsInRole);
    }
}
