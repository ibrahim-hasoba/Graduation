using System.Security.Claims;

namespace Graduation.API.Extensions
{
  public static class ClaimsPrincipalExtensions
  {
    public static string? GetUserId(this ClaimsPrincipal? user)
    {
      if (user == null) return null;

      // Prefer the standard NameIdentifier claim, fallback to custom "userId"
      var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (!string.IsNullOrEmpty(id)) return id;

      return user.FindFirst("userId")?.Value;
    }
  }
}
