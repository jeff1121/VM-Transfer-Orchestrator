namespace VMTO.API.Auth;

// JWT Token 產生服務介面
public interface IJwtTokenService
{
    string GenerateToken(string userId, string userName, string role);
}
