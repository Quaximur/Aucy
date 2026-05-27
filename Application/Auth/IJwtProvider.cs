namespace Aucy.Application.Auth;

public interface IJwtProvider
{
    public string GenerateToken(Guid userId);
}
