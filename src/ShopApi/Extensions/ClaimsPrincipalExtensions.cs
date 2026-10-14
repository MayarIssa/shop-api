using System.Security.Claims;

namespace ShopApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id, read from the JWT.</summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The token has no user id claim.");
        return int.Parse(value);
    }
}
