using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Aucy.Application.Auth;

public class JwtProvider(IOptions<JwtOptions> jwtoptions) : IJwtProvider
{
    private readonly JwtOptions _jwtoptions = jwtoptions.Value;

    public string GenerateToken(Guid userId)
    {
        var claims = new Claim[] {
            new("user_id", userId.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtoptions.Secretkey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(signingCredentials: credentials,
            claims: claims,
            expires: _jwtoptions.ExpireHours.HasValue ? DateTime.UtcNow.AddHours(_jwtoptions.ExpireHours.Value) : null);

        var tokenRaw = new JwtSecurityTokenHandler().WriteToken(token);

        return tokenRaw;
    }
}
