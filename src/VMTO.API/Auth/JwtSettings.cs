namespace VMTO.API.Auth;

// JWT 認證設定
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "vmto-api";
    public string Audience { get; set; } = "vmto-client";
    public string SecretKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
