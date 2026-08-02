using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Hato.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace Hato.Modules.People.Infrastructure;

public class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var user = User;
            if (user?.Identity?.IsAuthenticated != true) return null;

            var claim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public string? UserEmail => User?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? User?.FindFirst(ClaimTypes.Email)?.Value;

    public string? FullName => User?.FindFirst(ClaimTypes.Name)?.Value;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
}

public class SystemCurrentUser(Guid userId, string fullName, string email) : ICurrentUser
{
    public Guid? UserId => userId;
    public string? UserEmail => email;
    public string? FullName => fullName;
    public bool IsAuthenticated => true;
}
